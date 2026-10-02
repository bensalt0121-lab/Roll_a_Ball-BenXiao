/*********************************************************************************************
 * COMPONENT OF: Player
 * REQUIRED DEPENDENCIES: main camera, a pistol model (made by the setup tool), GameHUD,
 *                        WantedLevel, Input System package, gunshot sounds in
 *                        Assets/Resources/Audio/Weapons/Gunshot
 * DESCRIPTION: The pistol from the black market. Left click shoots where the camera is
 *              aiming (the crosshair). Shooting a person knocks them over; shooting at all
 *              makes the police come after you.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Pistol")]
    public bool hasPistol = false;
    // The gun model; it floats next to the player because the player ball spins
    public GameObject gunModel;
    public float range = 60f;
    // Seconds between shots
    public float fireDelay = 0.35f;
    public float knockForce = 6f;

    [Header("Crimes")]
    public int starsForShootingSomeone = 2;
    public int starsForShotsFired = 1;
    // Firing again within this many seconds does not add more "shots fired" stars
    public float shotsFiredCooldown = 10f;

    [Header("Effects")]
    public Light muzzleFlash;
    public LineRenderer bulletTrail;
    public float effectTime = 0.06f;

    private Camera aimCamera;
    private float nextShotTime;
    private float effectsOffTime;
    private float lastShotsFiredCrime = -100f;

    void Start()
    {
        aimCamera = Camera.main;
        ShowGun(hasPistol);
        HideEffects();
    }

    void Update()
    {
        CheckFire();
        TurnOffEffects();
    }

    // LateUpdate runs after the player and camera moved, so the gun never lags behind
    void LateUpdate()
    {
        HoldGun();
    }

    public void GivePistol()
    {
        hasPistol = true;
        ShowGun(true);
    }

    void ShowGun(bool show)
    {
        if (gunModel != null)
            gunModel.SetActive(show);

        GameHUD.ShowCrosshair(show);
    }

    // Keeps the gun at the player's right side, pointing where the camera looks
    void HoldGun()
    {
        if (!hasPistol || gunModel == null || aimCamera == null)
            return;

        float size = transform.lossyScale.y;
        gunModel.transform.position = transform.position + aimCamera.transform.right * (0.9f * size) + Vector3.up * (0.4f * size);
        gunModel.transform.rotation = aimCamera.transform.rotation;
    }

    void CheckFire()
    {
        if (!hasPistol || aimCamera == null || Mouse.current == null)
            return;

        // Only shoot while the mouse is locked to the game (not while clicking back in)
        if (Cursor.lockState != CursorLockMode.Locked || Time.time < nextShotTime)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            nextShotTime = Time.time + fireDelay;
            Shoot();
        }
    }

    // Fires a straight line from the middle of the screen and hits the first thing in the way
    void Shoot()
    {
        Ray aim = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 hitPoint = aim.origin + aim.direction * range;
        bool hitSomeone = false;

        RaycastHit[] hits = Physics.RaycastAll(aim, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            hitPoint = hit.point;
            hitSomeone = HitThing(hit);
            break;
        }

        if (!hitSomeone)
            ReportShotsFired();

        ShowEffects(hitPoint);
    }

    // Knocks over people, pushes physics objects; returns true if a person was hit
    bool HitThing(RaycastHit hit)
    {
        CityPerson person = hit.collider.GetComponentInParent<CityPerson>();
        if (person != null && !person.IsDown)
        {
            person.KnockOver(transform.position, knockForce, starsForShootingSomeone, "You shot someone");
            return true;
        }

        if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            hit.rigidbody.AddForceAtPosition((hit.point - transform.position).normalized * knockForce, hit.point, ForceMode.Impulse);

        return false;
    }

    void ReportShotsFired()
    {
        if (WantedLevel.Instance == null || Time.time - lastShotsFiredCrime < shotsFiredCooldown)
            return;

        lastShotsFiredCrime = Time.time;
        WantedLevel.Instance.ReportCrime(starsForShotsFired, "Shots fired", transform.position);
    }

    void ShowEffects(Vector3 hitPoint)
    {
        Vector3 muzzle = gunModel != null ? gunModel.transform.position + gunModel.transform.forward * 0.3f : transform.position;
        AudioClip bang = SoundLibrary.Random("Weapons/Gunshot");
        if (bang != null)
            AudioSource.PlayClipAtPoint(bang, muzzle);

        if (muzzleFlash != null)
            muzzleFlash.enabled = true;

        if (bulletTrail != null)
        {
            bulletTrail.SetPosition(0, muzzle);
            bulletTrail.SetPosition(1, hitPoint);
            bulletTrail.enabled = true;
        }

        effectsOffTime = Time.time + effectTime;
    }

    void TurnOffEffects()
    {
        if (Time.time >= effectsOffTime)
            HideEffects();
    }

    void HideEffects()
    {
        if (muzzleFlash != null)
            muzzleFlash.enabled = false;

        if (bulletTrail != null)
            bulletTrail.enabled = false;
    }
}
