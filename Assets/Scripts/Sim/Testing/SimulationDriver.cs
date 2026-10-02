/*********************************************************************************************
 * COMPONENT OF: "Simulation Driver" (made only by Tools > I Am A Ball > Run Test Simulation)
 * REQUIRED DEPENDENCIES: the gameplay made by Tools > I Am A Ball > Build Everything
 * DESCRIPTION: An automatic play tester. It checks that the NavMesh, people, traffic,
 *              shops, every job, the police, the black market, the hospital, music and
 *              buying a home all work, takes pictures with the HUD showing, and writes
 *              results.txt. Not used in the game.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Tests for one job at a time, the trash, taxi, clerk and street show jobs, the
 *              black market, pistol, police cars, hospital and music. A test that crashes
 *              is written down as a FAIL and the other tests still run.
 *********************************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

public class SimulationDriver : MonoBehaviour
{
    public string outputFolder;
    public Action onFinished;

    // Spots to check around a trash bag spot (the job puts each bag up to 1 m away from its spot)
    private static readonly Vector3[] BagSearchOffsets =
    {
        Vector3.zero,
        new Vector3(0.7f, 0f, 0.7f),
        new Vector3(-0.7f, 0f, 0.7f),
        new Vector3(0.7f, 0f, -0.7f),
        new Vector3(-0.7f, 0f, -0.7f),
    };

    private readonly StringBuilder report = new StringBuilder();
    private int passed;
    private int failed;
    private PlayerInteractor player;
    private MoneyManager money;
    private PlayerNeeds needs;

    IEnumerator Start()
    {
        Log("Simulation started " + DateTime.Now);
        yield return new WaitForSeconds(3f);

        player = FindAnyObjectByType<PlayerInteractor>();
        money = FindAnyObjectByType<MoneyManager>();
        needs = FindAnyObjectByType<PlayerNeeds>();

        Check("Player has PlayerInteractor", player != null);
        Check("MoneyManager exists", money != null);
        Check("Player has PlayerNeeds", needs != null);
        Check("HUD exists", GameHUD.Instance != null);
        Check("WantedLevel exists", WantedLevel.Instance != null);
        Check("NavMesh was built", NavMesh.CalculateTriangulation().vertices.Length > 0);

        if (player == null || money == null || needs == null)
        {
            Finish();
            yield break;
        }

        yield return RunTest("People and traffic", TestPeopleAndTraffic());
        yield return RunTest("Hunger and thirst", TestNeedsDrain());
        yield return RunTest("Burger shop", TestShop());
        yield return RunTest("Delivery job", TestDeliveryJob());
        yield return RunTest("Knocking people over and the police", TestKnockOverAndPolice());
        yield return RunTest("Buying a home", TestBuyHome());

        // Version 1.1 tests. The jobs go first, then the tests that make the player wanted.
        yield return RunTest("Only one job at a time", TestOnlyOneJobAtATime());
        yield return RunTest("Trash pickup job", TestTrashJob());
        yield return RunTest("Taxi job", TestRideJob());
        yield return RunTest("Store clerk job", TestClerkJob());
        yield return RunTest("Street performer job", TestPerformerJob());
        yield return RunTest("Black market", TestBlackMarket());
        yield return RunTest("Pistol", TestPistolHit());
        yield return RunTest("Police cars", TestPoliceCars());
        yield return RunTest("Hospital", TestHospital());
        yield return RunTest("Music", TestMusic());

        // One last tidy-up (this also reports a job the last test left running)
        GetReadyForNextTest();
        Finish();
    }

    IEnumerator TestPeopleAndTraffic()
    {
        NpcWalker[] people = FindObjectsByType<NpcWalker>();
        TrafficCar[] cars = FindObjectsByType<TrafficCar>();
        int onNavMesh = people.Count(p => p.GetComponent<NavMeshAgent>().isOnNavMesh);
        Check("People standing on the NavMesh: " + onNavMesh + "/" + people.Length, people.Length > 0 && onNavMesh >= people.Length * 0.8f);

        Vector3[] peopleBefore = people.Select(p => p.transform.position).ToArray();
        Vector3[] carsBefore = cars.Select(c => c.transform.position).ToArray();
        TakePicture("sim_01_start", player.transform.position);
        yield return new WaitForSeconds(6f);

        int peopleMoved = people.Where((p, i) => Vector3.Distance(p.transform.position, peopleBefore[i]) > 0.5f).Count();
        int carsMoved = cars.Where((c, i) => Vector3.Distance(c.transform.position, carsBefore[i]) > 2f).Count();
        Check("People walking around: " + peopleMoved + "/" + people.Length, peopleMoved >= people.Length / 2);
        Check("Cars driving: " + carsMoved + "/" + cars.Length, cars.Length > 0 && carsMoved >= cars.Length / 2);

        if (cars.Length > 0)
            TakePicture("sim_02_traffic", cars[0].transform.position);
    }

    IEnumerator TestNeedsDrain()
    {
        float hunger = needs.Hunger;
        float thirst = needs.Thirst;
        yield return new WaitForSeconds(1f);
        Check("Hunger and thirst go down over time", needs.Hunger < hunger && needs.Thirst < thirst);
    }

    IEnumerator TestShop()
    {
        Shop shop = FindObjectsByType<Shop>().FirstOrDefault(s => s.itemName == "Burger");
        Check("Burger counter exists", shop != null);
        if (shop == null)
            yield break;

        money.AddMoney(100);
        needs.Eat(-60f);
        int moneyBefore = money.currentMoney;
        float hungerBefore = needs.Hunger;

        Teleport(shop.transform.position + shop.transform.forward * 0.6f);
        yield return new WaitForSeconds(0.5f);
        Check("Standing at the counter shows the buy prompt", GameHUD.Instance != null && GameHUD.Instance.promptText != null && GameHUD.Instance.promptText.text.Contains("Burger"));
        TakePicture("sim_03_burger_shop", shop.transform.position);

        shop.Interact(player);
        Check("Buying a burger costs $" + shop.price, money.currentMoney == moneyBefore - shop.price);
        Check("Buying a burger fills hunger", needs.Hunger > hungerBefore);

        money.RemoveMoney(money.currentMoney);
        shop.Interact(player);
        Check("Cannot buy with no money", money.currentMoney == 0);
    }

    IEnumerator TestDeliveryJob()
    {
        DeliveryJob job = FindAnyObjectByType<DeliveryJob>();
        Check("Delivery job exists", job != null);
        if (job == null)
            yield break;

        Check("Delivery job has drop-off spots: " + (job.dropOffPoints != null ? job.dropOffPoints.Length : 0), job.dropOffPoints != null && job.dropOffPoints.Length > 3);
        Teleport(job.transform.position + job.transform.forward * 0.8f);
        yield return new WaitForSeconds(0.3f);
        TakePicture("sim_04_depot", job.transform.position);

        job.Interact(player);
        Check("Starting a delivery works", job.IsActive && job.marker != null && job.marker.activeSelf);
        if (!job.IsActive || job.marker == null)
            yield break;

        int moneyBefore = money.currentMoney;
        Vector3 target = job.marker.transform.position;
        Teleport(target + Vector3.right * 4f);
        yield return new WaitForSeconds(0.5f);
        TakePicture("sim_05_delivery_marker", target);

        Teleport(target);
        yield return new WaitForSeconds(0.5f);
        Check("Finishing a delivery pays money (+" + (money.currentMoney - moneyBefore) + ")", !job.IsActive && money.currentMoney > moneyBefore);
    }

    IEnumerator TestKnockOverAndPolice()
    {
        NpcWalker person = FindObjectsByType<NpcWalker>().FirstOrDefault(p => p.GetComponent<NavMeshAgent>().isOnNavMesh);
        PoliceOfficer[] police = FindObjectsByType<PoliceOfficer>();
        Check("Police officers exist: " + police.Length, police.Length > 0);
        if (person == null)
            yield break;

        // Roll the player into a person (keyboard movement is paused so it does not cancel the push)
        Rigidbody body = player.GetComponent<Rigidbody>();
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;

        Vector3 behind = person.transform.position - person.transform.forward * 1.2f;
        Teleport(new Vector3(behind.x, person.transform.position.y, behind.z));
        for (int i = 0; i < 40 && !person.IsDown; i++)
        {
            Vector3 direction = person.transform.position - player.transform.position;
            direction.y = 0f;
            body.linearVelocity = direction.normalized * 5f;
            yield return new WaitForFixedUpdate();
        }

        if (movement != null)
            movement.enabled = true;
        yield return new WaitForSeconds(0.3f);

        Check("Rolling into a person knocks them over", person.IsDown);
        Check("Knocking someone over makes you wanted (" + WantedLevel.Instance.Stars + " stars)", WantedLevel.Instance.Stars > 0);
        TakePicture("sim_06_knocked_over", person.transform.position);

        if (police.Length == 0 || WantedLevel.Instance.Stars == 0)
            yield break;

        // See if the police come after the player
        PoliceOfficer cop = police[0];
        float before = Vector3.Distance(cop.transform.position, player.transform.position);
        yield return new WaitForSeconds(4f);
        float after = Vector3.Distance(cop.transform.position, player.transform.position);
        bool busted = WantedLevel.Instance.Stars == 0;
        Check("Police chase the player (" + before.ToString("0.0") + "m -> " + after.ToString("0.0") + "m" + (busted ? ", busted" : "") + ")", after < before - 1f || busted);
        TakePicture("sim_07_police_chase", player.transform.position);

        // Let the officer catch a player who stands still right next to them
        if (!busted)
        {
            WantedLevel.Instance.ReportCrime(1, "Test", player.transform.position);
            Teleport(cop.transform.position + cop.transform.forward * 0.6f);
            yield return new WaitForSeconds(1.5f);
        }
        Check("Police can bust the player", WantedLevel.Instance.Stars == 0);
    }

    IEnumerator TestBuyHome()
    {
        HomeProperty house = FindObjectsByType<HomeProperty>().FirstOrDefault(h => !h.Owned && !h.isDreamHome);
        Check("A house is for sale", house != null);
        if (house == null)
            yield break;

        Teleport(house.transform.position + house.transform.forward * 0.8f);
        yield return new WaitForSeconds(0.3f);
        TakePicture("sim_08_house_for_sale", house.transform.position);

        money.AddMoney(house.price);
        house.Interact(player);
        Check("Buying " + house.propertyName + " works", house.Owned && HomeProperty.CurrentHome == house);
        yield return new WaitForSeconds(0.3f);
        TakePicture("sim_09_house_bought", house.transform.position);
    }

    // ---------- Version 1.1 tests ----------

    // While doing a delivery, pressing E at another job must NOT start it
    IEnumerator TestOnlyOneJobAtATime()
    {
        DeliveryJob delivery = FindAnyObjectByType<DeliveryJob>();
        Job otherJob = FindObjectsByType<Job>().FirstOrDefault(j => !(j is DeliveryJob));
        Check("A delivery job and another kind of job exist", delivery != null && otherJob != null);
        if (delivery == null || otherJob == null)
            yield break;

        Teleport(InFrontOf(delivery.transform, 0.8f));
        yield return new WaitForSeconds(0.3f);
        delivery.Interact(player);
        Check("Delivery started for the one-job test", delivery.IsActive);
        if (!delivery.IsActive)
            yield break;

        // Go to the other job and press E there
        Teleport(InFrontOf(otherJob.transform, 0.8f));
        yield return new WaitForSeconds(0.3f);
        otherJob.Interact(player);
        Check("Cannot start the " + otherJob.jobName + " job during a delivery", Job.Active == delivery && !otherJob.IsActive);
        Check("The " + otherJob.jobName + " job says to finish the delivery first", otherJob.GetPrompt(player).Contains("first"));
        yield return null;
        TakePicture("sim_10_one_job_at_a_time", otherJob.transform.position);

        // Clean up: finish the delivery the normal way, by rolling onto the green marker
        if (delivery.marker != null)
        {
            Teleport(delivery.marker.transform.position);
            yield return WaitFor(() => !delivery.IsActive, 2f);
        }
        Check("The delivery can still be finished afterwards", Job.Active == null);
    }

    // Trash pickup: start the job, roll onto every bag, come back and get paid
    IEnumerator TestTrashJob()
    {
        TrashJob job = FindAnyObjectByType<TrashJob>();
        Check("Trash pickup job exists", job != null);
        if (job == null)
            yield break;

        bool ready = job.bagPrefab != null && job.bagSpots != null && job.bagSpots.Length > 0;
        Check("Trash job has a bag prefab and bag spots (" + (job.bagSpots != null ? job.bagSpots.Length : 0) + " spots)", ready);
        if (!ready)
            yield break;

        Teleport(InFrontOf(job.transform, 0.8f));
        yield return new WaitForSeconds(0.3f);
        int moneyBefore = money.currentMoney;
        job.Interact(player);
        Check("Starting the trash job puts bags around the city (" + job.BagsLeft + " bags)", job.IsActive && job.BagsLeft > 0);
        if (!job.IsActive)
            yield break;

        // A picture of the first bag from a few meters away
        List<Transform> bags = FindCopiesOf(job.bagPrefab);
        if (bags.Count > 0)
        {
            Vector3 firstBag = bags[0].position;
            Teleport(WalkableSpotNear(firstBag + Vector3.back * 3f));
            yield return new WaitForSeconds(0.2f);
            TakePicture("sim_11_trash_bag", firstBag);
        }

        // Roll onto each bag: the ball is moved straight onto it, and the job picks it up
        foreach (Transform bag in bags)
        {
            if (bag == null)   // already picked up
                continue;

            Teleport(bag.position);
            yield return null;
            yield return null;
        }

        // Backup plan if a bag was missed: visit every spot a bag can appear at
        if (job.BagsLeft > 0)
        {
            foreach (Transform spot in job.bagSpots)
            {
                foreach (Vector3 offset in BagSearchOffsets)
                {
                    if (spot == null || job.BagsLeft == 0)
                        break;

                    Teleport(spot.position + offset);
                    yield return null;
                    yield return null;
                }
            }
        }
        Check("Every trash bag was picked up (" + job.BagsLeft + " left)", job.BagsLeft == 0);

        // Hand the bags in at the job board
        Teleport(InFrontOf(job.transform, 0.8f));
        yield return new WaitForSeconds(0.3f);
        job.Interact(player);
        Check("Handing in the bags pays money (+" + (money.currentMoney - moneyBefore) + ") and ends the job", Job.Active == null && money.currentMoney > moneyBefore);
        TakePicture("sim_12_trash_paid", job.transform.position);
    }

    // Taxi: the passenger follows the player to the yellow marker
    IEnumerator TestRideJob()
    {
        RideJob job = FindAnyObjectByType<RideJob>();
        Check("Taxi job exists", job != null);
        if (job == null)
            yield break;

        bool ready = job.passenger != null && job.marker != null && job.destinations != null && job.destinations.Length > 0;
        Check("Taxi job has a passenger, a marker and destinations", ready);
        if (!ready)
            yield break;

        ScriptedWalker passenger = job.passenger;
        Teleport(InFrontOf(job.transform, 0.8f));
        yield return new WaitForSeconds(0.5f);

        int moneyBefore = money.currentMoney;
        job.Interact(player);
        Check("Starting a taxi ride shows the yellow marker", job.IsActive && job.marker.activeSelf);
        if (!job.IsActive)
            yield break;

        // Roll 7 m toward the destination; the passenger should walk after the player
        Vector3 towardMarker = job.marker.transform.position - passenger.transform.position;
        towardMarker.y = 0f;
        Teleport(WalkableSpotNear(passenger.transform.position + towardMarker.normalized * 7f));
        yield return null;
        float before = Vector3.Distance(passenger.transform.position, player.transform.position);
        yield return new WaitForSeconds(3f);
        float after = Vector3.Distance(passenger.transform.position, player.transform.position);
        Check("The passenger follows the player (" + before.ToString("0.0") + "m -> " + after.ToString("0.0") + "m)", after < before - 1f);
        TakePicture("sim_13_taxi_passenger_following", passenger.transform.position);
        if (!job.IsActive)
            yield break;

        // Skip the long walk: put the passenger on the marker and the player right next to them.
        // Both move in the same frame, so the job never thinks the passenger got lost.
        Vector3 dropOff = WalkableSpotNear(job.marker.transform.position);
        Teleport(WalkableSpotNear(dropOff + Vector3.right * 1.5f));
        passenger.ResetTo(dropOff);
        passenger.FollowTarget(player.transform, 1.8f);
        TakePicture("sim_14_taxi_destination", dropOff);

        yield return WaitFor(() => !job.IsActive, 2f);
        Check("Bringing the passenger to the marker pays money (+" + (money.currentMoney - moneyBefore) + ") and ends the job", Job.Active == null && money.currentMoney > moneyBefore);
    }

    // Store clerk: stay at the counter and serve every customer who walks up
    IEnumerator TestClerkJob()
    {
        ClerkJob job = FindAnyObjectByType<ClerkJob>();
        Check("Store clerk job exists", job != null);
        if (job == null)
            yield break;

        bool ready = job.customer != null && job.customerSpawn != null && job.customerSpot != null;
        Check("Clerk job has a customer and the spots they walk between", ready);
        if (!ready)
            yield break;

        // Stand right beside the counter, out of the customer's way
        Vector3 counterSpot = WalkableSpotNear(job.transform.position - job.transform.right * 1.2f);
        Teleport(counterSpot);
        yield return new WaitForSeconds(0.3f);

        int moneyBefore = money.currentMoney;
        job.Interact(player);
        Check("Starting a clerk shift works", job.IsActive);
        if (!job.IsActive)
            yield break;

        int served = 0;
        while (job.IsActive && served < job.customersPerShift)
        {
            // Wait (up to 15 seconds) for the next customer, staying at the counter the whole time
            Teleport(counterSpot);
            float giveUpAt = Time.time + 15f;
            while (job.IsActive && !CustomerIsWaiting(job) && Time.time < giveUpAt)
            {
                if (SimUtil.FlatDistance(player.transform.position, counterSpot) > 1f)
                    Teleport(counterSpot);
                yield return new WaitForSeconds(0.2f);
            }

            if (!CustomerIsWaiting(job))
            {
                Check("Customer " + (served + 1) + " reached the counter within 15 seconds", false);
                break;
            }

            if (served == 0)
                TakePicture("sim_15_clerk_customer", job.customer.transform.position);

            job.Interact(player);
            served++;
            yield return null;
        }

        Check("Customers served: " + served + "/" + job.customersPerShift, served >= job.customersPerShift);
        Check("Finishing the shift pays money (+" + (money.currentMoney - moneyBefore) + ") and ends the job", Job.Active == null && money.currentMoney > moneyBefore);
        TakePicture("sim_16_clerk_shift_done", job.transform.position);
    }

    // The clerk job shows "Press E to serve the customer" while someone is waiting at the counter
    bool CustomerIsWaiting(ClerkJob job)
    {
        return job.IsActive && job.GetPrompt(player).Contains("serve");
    }

    // Street show: every landing on the stage is a trick, and people come to watch
    IEnumerator TestPerformerJob()
    {
        PerformerJob job = FindAnyObjectByType<PerformerJob>();
        Check("Street performer job exists", job != null);
        if (job == null)
            yield break;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Check("Player has PlayerMovement (it tells the show when the ball lands)", movement != null);
        if (movement == null)
            yield break;

        // Drop the ball onto the middle of the stage
        Vector3 stage = job.transform.position;
        Teleport(stage + Vector3.up * 1f);
        yield return WaitFor(() => movement.IsGrounded, 3f);
        Check("The stage counts as ground (IsGrounded)", movement.IsGrounded);

        // A short 8 second show for the test. showLength is only read when the show starts,
        // so it is put back to normal right after starting.
        const float testShowLength = 8f;
        float normalShowLength = job.showLength;
        job.showLength = testShowLength;
        int moneyBefore = money.currentMoney;
        job.Interact(player);
        job.showLength = normalShowLength;
        Check("Starting the street show works", job.IsActive);
        if (!job.IsActive)
            yield break;

        // Batch mode has no keyboard, so each jump is faked: lift the ball 1.5 m and let it fall
        int landings = 0;
        for (int i = 0; i < 3 && job.IsActive; i++)
        {
            SimUtil.Teleport(player.transform, new Vector3(stage.x, player.transform.position.y + 1.5f, stage.z));
            yield return WaitFor(() => !movement.IsGrounded, 1f);
            yield return WaitFor(() => movement.IsGrounded, 3f);
            if (movement.IsGrounded)
                landings++;

            yield return new WaitForSeconds(0.2f);
            if (i == 0)
                TakePicture("sim_17_street_show", stage);
        }
        Check("Jumps land back on the stage: " + landings + "/3", landings == 3);

        // Wait for the show to end by itself
        yield return WaitFor(() => !job.IsActive, testShowLength + 3f);
        Check("The show ends by itself after " + testShowLength + " seconds", !job.IsActive);
        Check("The show pays money (+" + (money.currentMoney - moneyBefore) + ")", money.currentMoney > moneyBefore);
        if (GameHUD.Instance != null && GameHUD.Instance.messageText != null)
            Info("Show result: " + GameHUD.Instance.messageText.text);
        TakePicture("sim_18_show_over", stage);
    }

    // Black market: climb into the sewer, buy a pistol and a disguise, climb back out
    IEnumerator TestBlackMarket()
    {
        Teleporter[] teleporters = FindObjectsByType<Teleporter>();
        Teleporter sewerEntrance = teleporters.FirstOrDefault(t => TextHas(t.prompt, "sewer") && t.transform.position.y > -5f);
        Teleporter ladderUp = teleporters.FirstOrDefault(t => t != sewerEntrance && (TextHas(t.prompt, "up") || TextHas(t.prompt, "leave")));
        Check("Sewer entrance exists", sewerEntrance != null);
        if (sewerEntrance == null)
            yield break;

        Teleport(InFrontOf(sewerEntrance.transform, 0.8f));
        yield return new WaitForSeconds(0.3f);
        TakePicture("sim_19_sewer_entrance", sewerEntrance.transform.position);

        sewerEntrance.Interact(player);
        yield return new WaitForSeconds(0.5f);
        Check("Climbing into the sewer takes you underground (height " + player.transform.position.y.ToString("0.0") + ")", player.transform.position.y < -10f);

        // The pistol
        PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
        BlackMarketCounter[] counters = FindObjectsByType<BlackMarketCounter>();
        BlackMarketCounter pistolCounter = counters.FirstOrDefault(c => c.item == BlackMarketCounter.Item.Pistol);
        Check("Player has PlayerWeapon", weapon != null);
        Check("Pistol counter exists", pistolCounter != null);
        if (weapon != null && pistolCounter != null)
        {
            Teleport(InFrontOf(pistolCounter.transform, 0.8f));
            yield return new WaitForSeconds(0.3f);

            bool hadPistol = weapon.hasPistol;
            money.AddMoney(pistolCounter.price);
            int moneyBefore = money.currentMoney;
            pistolCounter.Interact(player);
            Check("Buying a pistol works ($" + pistolCounter.price + ")", weapon.hasPistol && (hadPistol || money.currentMoney == moneyBefore - pistolCounter.price));
            yield return null;
            TakePicture("sim_20_bought_pistol", pistolCounter.transform.position);
        }

        // The disguise (makes the police forget you)
        BlackMarketCounter disguiseCounter = counters.FirstOrDefault(c => c.item == BlackMarketCounter.Item.Disguise);
        Check("Disguise counter exists", disguiseCounter != null);
        if (disguiseCounter != null && WantedLevel.Instance != null)
        {
            Teleport(InFrontOf(disguiseCounter.transform, 0.8f));
            yield return new WaitForSeconds(0.3f);

            WantedLevel.Instance.ReportCrime(2, "Test crime", player.transform.position);
            int starsBefore = WantedLevel.Instance.Stars;
            money.AddMoney(disguiseCounter.price);
            disguiseCounter.Interact(player);
            Check("Buying a disguise clears the wanted stars (" + starsBefore + " -> " + WantedLevel.Instance.Stars + ")", starsBefore > 0 && WantedLevel.Instance.Stars == 0);
            yield return null;
            TakePicture("sim_21_bought_disguise", disguiseCounter.transform.position);
        }

        // Climb the ladder back up to the street
        Check("Ladder back up exists", ladderUp != null);
        if (ladderUp != null)
        {
            Teleport(InFrontOf(ladderUp.transform, 0.8f));
            yield return new WaitForSeconds(0.3f);
            ladderUp.Interact(player);
            yield return new WaitForSeconds(0.5f);
        }
        Check("Climbing the ladder brings you back to the street (height " + player.transform.position.y.ToString("0.0") + ")", player.transform.position.y > -5f);

        // Never leave the player stuck underground for the next tests
        if (player.transform.position.y < -5f)
            Teleport(InFrontOf(sewerEntrance.transform, 2f));

        TakePicture("sim_22_back_on_street", player.transform.position);
    }

    // Pistol: a hit knocks the person over and gives 2 wanted stars
    IEnumerator TestPistolHit()
    {
        PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
        Check("Player has the pistol from the black market", weapon != null && weapon.hasPistol);

        // Real shooting needs a left mouse click while the mouse is locked to the game, and batch
        // mode has no mouse. So this test calls KnockOver the same way the pistol does when a
        // bullet hits someone (see PlayerWeapon.HitThing).
        NpcWalker target = FindObjectsByType<NpcWalker>().FirstOrDefault(p => !p.IsDown);
        Check("Someone to shoot at exists", target != null);
        if (target == null || WantedLevel.Instance == null)
            yield break;

        Teleport(WalkableSpotNear(target.transform.position + target.transform.right * 3f));
        yield return new WaitForSeconds(0.3f);

        target.KnockOver(player.transform.position, 6f, 2, "You shot someone");
        int stars = WantedLevel.Instance.Stars;
        yield return new WaitForSeconds(0.3f);

        Check("Shooting someone knocks them over", target.IsDown);
        Check("Shooting someone gives 2 or more wanted stars (" + stars + ")", stars >= 2);
        TakePicture("sim_23_shot_someone", target.transform.position);
    }

    // Police cars leave their parking spots and chase the player at 3 or more stars
    IEnumerator TestPoliceCars()
    {
        PoliceCar[] cars = FindObjectsByType<PoliceCar>();
        Check("Police cars exist: " + cars.Length, cars.Length > 0);
        if (cars.Length == 0 || WantedLevel.Instance == null)
            yield break;

        Vector3[] parkedAt = cars.Select(c => c.transform.position).ToArray();
        WantedLevel.Instance.ReportCrime(3, "Test: big crime", player.transform.position);
        Check("The player has 3 or more wanted stars (" + WantedLevel.Instance.Stars + ")", WantedLevel.Instance.Stars >= 3);

        // About 5 seconds, or less if the police catch the player first
        yield return WaitFor(() => WantedLevel.Instance.Stars == 0, 5f);

        bool busted = WantedLevel.Instance.Stars == 0;
        int carsMoved = cars.Where((c, i) => Vector3.Distance(c.transform.position, parkedAt[i]) > 3f).Count();
        Check("Police cars chase the player (" + carsMoved + "/" + cars.Length + " drove off" + (busted ? ", busted" : "") + ")", carsMoved > 0 || busted);

        PoliceCar closest = cars.OrderBy(c => Vector3.Distance(c.transform.position, player.transform.position)).First();
        TakePicture("sim_24_police_car_chase", closest.transform.position);
    }

    // Hospital: pay the bill and wake up outside
    IEnumerator TestHospital()
    {
        Hospital hospital = Hospital.Instance;
        Check("Hospital exists", hospital != null);
        if (hospital == null)
            yield break;

        Check("Hospital has a release point", hospital.releasePoint != null);

        // Give the player money first, so there is a bill to pay
        money.AddMoney(100);
        int moneyBefore = money.currentMoney;
        hospital.Admit(player, "Test: hit by a car");
        yield return new WaitForSeconds(0.3f);

        int bill = moneyBefore - money.currentMoney;
        Check("The hospital charges a bill ($" + bill + ")", bill > 0);

        if (hospital.releasePoint != null)
        {
            float distance = SimUtil.FlatDistance(player.transform.position, hospital.releasePoint.position);
            Check("The player wakes up outside the hospital (" + distance.ToString("0.0") + "m from the release point)", distance < 3f);
            TakePicture("sim_25_hospital", hospital.releasePoint.position);
        }
    }

    // Music: the MusicPlayer and its speaker exist (music files may not be added yet)
    IEnumerator TestMusic()
    {
        MusicPlayer musicPlayer = FindAnyObjectByType<MusicPlayer>();
        Check("MusicPlayer exists", musicPlayer != null);
        if (musicPlayer == null)
            yield break;

        Check("MusicPlayer has its music AudioSource", musicPlayer.music != null);
        if (musicPlayer.music == null)
            yield break;

        // A fresh project may have no songs yet, so silence is only a note, not a FAIL
        AudioClip song = musicPlayer.music.clip;
        if (musicPlayer.music.isPlaying && song != null)
            Check("Music is playing: " + song.name, true);
        else
            Info("No music is playing (are there songs in Assets/Resources/Audio/Music/City?)");
    }

    // ---------- Running tests safely ----------

    // Runs one test. If the test hits an error, it is written down as a FAIL and the next test
    // still runs, so one broken feature never stops the whole simulation.
    // (C# does not allow "yield return" inside try/catch, so each step of the test runs
    // inside the try, and the waiting between steps happens outside it.)
    IEnumerator RunTest(string testName, IEnumerator test)
    {
        GetReadyForNextTest();
        Log("---- " + testName + " ----");

        while (true)
        {
            object nextWait = null;
            try
            {
                if (!test.MoveNext())
                    break;

                nextWait = test.Current;
            }
            catch (Exception error)
            {
                Check(testName + " test crashed: " + error.GetType().Name + " - " + error.Message, false);
                break;
            }

            yield return nextWait;
        }
    }

    // Puts the game back to normal so one test cannot break the next one
    void GetReadyForNextTest()
    {
        // No wanted stars, so the police do not grab the player in the middle of a test
        if (WantedLevel.Instance != null)
            WantedLevel.Instance.ClearStars();

        // Some tests switch keyboard movement off; make sure it is back on
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = true;

        // Only one job can run at a time, so a job left running would block every later job test
        Job stuckJob = Job.Active;
        if (stuckJob != null)
        {
            Check("The " + stuckJob.jobName + " job was finished by its test (it was still running, so it is stopped now)", false);

            // Switching a job off clears Job.Active (see Job.OnDisable), then it is switched back on
            stuckJob.enabled = false;
            stuckJob.enabled = true;
            GameHUD.ClearObjective();
        }
    }

    // ---------- Helpers ----------

    void Teleport(Vector3 position)
    {
        Rigidbody body = player.GetComponent<Rigidbody>();
        position.y += 0.4f;
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
        }
        player.transform.position = position;
    }

    // A spot a little in front of something (where a player would stand to use it)
    Vector3 InFrontOf(Transform thing, float distance)
    {
        return thing.position + thing.forward * distance;
    }

    // The closest spot on the NavMesh (where people can walk, so not inside a wall).
    // If there is no NavMesh nearby, the spot is used as it is.
    Vector3 WalkableSpotNear(Vector3 spot)
    {
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return hit.position;

        return spot;
    }

    // Unity names every copy of a prefab "<prefab name>(Clone)", so this finds the copies in the scene
    List<Transform> FindCopiesOf(GameObject prefab)
    {
        List<Transform> copies = new List<Transform>();
        if (prefab == null)
            return copies;

        string copyName = prefab.name + "(Clone)";
        foreach (Transform thing in FindObjectsByType<Transform>())
        {
            if (thing.name == copyName)
                copies.Add(thing);
        }
        return copies;
    }

    // Waits until 'done' is true, but never longer than 'maxSeconds'
    IEnumerator WaitFor(Func<bool> done, float maxSeconds)
    {
        float giveUpAt = Time.time + maxSeconds;
        while (!done() && Time.time < giveUpAt)
            yield return null;
    }

    // True if 'text' contains 'word', ignoring upper and lower case (and safe if text is empty)
    bool TextHas(string text, string word)
    {
        return text != null && text.ToLower().Contains(word);
    }

    void Check(string description, bool ok)
    {
        if (ok) passed++; else failed++;
        Log((ok ? "PASS  " : "FAIL  ") + description);
    }

    // A note in the results that is neither a PASS nor a FAIL
    void Info(string description)
    {
        Log("INFO  " + description);
    }

    void Log(string line)
    {
        report.AppendLine(line);
        Debug.Log("[Simulation] " + line);
    }

    // Takes a picture looking at 'focus' with the HUD drawn on top
    void TakePicture(string name, Vector3 focus)
    {
        if (string.IsNullOrEmpty(outputFolder))
            return;

        GameObject go = new GameObject("Simulation Camera");
        Camera cam = go.AddComponent<Camera>();
        Vector3 from = player.transform.position - (focus - player.transform.position).normalized * 4f;
        if (Vector3.Distance(focus, player.transform.position) < 1f)
            from = focus + new Vector3(-5f, 0f, -5f);

        cam.transform.position = new Vector3(from.x, focus.y + 3f, from.z);
        cam.transform.LookAt(focus + Vector3.up * 1f);
        cam.fieldOfView = 60f;

        int minimapLayer = LayerMask.NameToLayer("Minimap");
        if (minimapLayer >= 0)
            cam.cullingMask = ~(1 << minimapLayer);

        Canvas hud = GameHUD.Instance != null ? GameHUD.Instance.GetComponent<Canvas>() : null;
        if (hud != null)
        {
            hud.renderMode = RenderMode.ScreenSpaceCamera;
            hud.worldCamera = cam;
            hud.planeDistance = 1f;
        }

        RenderTexture rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();

        RenderTexture.active = rt;
        Texture2D picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        picture.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(outputFolder, name + ".png"), picture.EncodeToPNG());

        if (hud != null)
            hud.renderMode = RenderMode.ScreenSpaceOverlay;

        cam.targetTexture = null;
        rt.Release();
        Destroy(rt);
        Destroy(picture);
        Destroy(go);
    }

    void Finish()
    {
        Log("RESULT: " + passed + " passed, " + failed + " failed");
        if (!string.IsNullOrEmpty(outputFolder))
            File.WriteAllText(Path.Combine(outputFolder, "results.txt"), report.ToString());

        onFinished?.Invoke();
    }
}
