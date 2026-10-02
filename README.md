# I Am a Ball!

A low poly city life sim made in Unity for my game design class (Roll-a-Ball project).
A human pretends to be a ball and rolls around the city: get a job, keep fed, stay out of
trouble with the police, and save up for a better home.

Made by Ben Xiao, with help from Claude (AI coding assistant).

## How to play

| Key | What it does |
| --- | --- |
| W A S D | Roll (in the direction the camera faces) |
| Mouse | Look around |
| Space | Jump |
| E | Use things: buy food, start or finish a job, buy a house, sleep, climb into the sewer |
| Left click | Shoot (after buying a pistol) / lock the mouse |
| Esc | Free the mouse |
| H | Help screen (controls, goal, map colors). R in the help screen replays the tutorial |
| T | Skip the tutorial |
| M | Big map (the game pauses): every place is labeled, plus a note that tells you what to do next |
| Enter | Next tutorial tip |

A short **tutorial** starts the first time you play: move, jump, buy food, do a job, then the
goal and the police. A blue beam shows where to go. You start with $40.

Tall glowing beams show where to go: blue = tutorial, green = delivery, yellow = taxi
passenger, purple = the next house you can buy.

- **Hunger and thirst** go down slowly (about 7-8 minutes). Buy food and drinks at the Burger
  Shop, Drink Bar or Corner Store. If a bar is empty you roll slower.
- **Jobs** (green on the minimap, one at a time):
  - *Delivery* (Delivery Depot): roll the package to the green marker. Farther = more money.
  - *Trash Pickup* (next to the garbage truck at the depot): collect the trash bags around the city, then come back.
  - *Taxi* (Downtown taxi stand): a passenger follows you; lead them to the yellow marker.
  - *Store Clerk* (Corner Store staff counter): press E to serve each customer who walks up.
  - *Street Performer* (Food Court stage): jump around on the stage for 30 seconds; people come to watch.
- **Homes:** you start with the Starter Apartment (press E at the door to sleep until morning).
  Buy the House on Oak Street ($300), then the Dream Villa ($800) to win.
- **Police:** bumping into people is fine, but jumping into people or shooting makes you wanted
  (stars, top right). Officers chase you on foot; at 4 stars police cars join in. Get far away
  and they give up. If they catch you, you pay a fine.
  Stars go away if you stay out of trouble.
- **Hospital:** if a car hits you, you wake up at the hospital and pay a bill.
- **Black market:** hidden under the sewer entrance at the south-east corner of the city.
  Sells a pistol ($150) and a disguise ($60) that makes the police forget you.
- **Money** is also lying around the city. The minimap shows shops (orange), jobs (green),
  homes (yellow/purple), the police station (blue) and the hospital (red).
- **Music** changes with what is happening: city, black market, and police chase.

## If the game is slow (school laptop)

| Key | What it does |
| --- | --- |
| F1 | Change graphics: Low / Medium / High (remembered next time) |
| F3 | Show / hide the FPS counter |

The first time the game starts it picks a setting for the computer (built-in Intel/AMD
graphics get Low). Low makes the picture a bit softer, draws less far (with fog), hides small
things (trees, people, cars) when they are far away, and lets fewer street lights glow.
Street lights only turn on at night and only near you. The minimap redraws a few times per
second instead of every frame.

In the editor, **Tools > I Am A Ball > Optimize For Slow Laptops** turns off street light
shadows, shrinks 4K textures to 1K, bakes the NavMesh (faster start) and bakes occlusion
culling (things hidden behind buildings are not drawn). Build Everything does this too.
Save the scene afterwards.

## Adding music and sounds

Drop .mp3/.ogg/.wav files into the folders in `Assets/Resources/Audio` (Music/City,
Music/BlackMarket, Music/Wanted, Cars/Engine, Cars/Horn, Cars/Siren, City/Ambience,
Weapons/Gunshot). The game uses them automatically. Write the credit in
`Assets/Resources/Audio/AUDIO_CREDITS.txt`.

## Opening the project

- Unity **6000.5.10f1** (Unity 6.5). Open the folder with Unity Hub > Add > Add project from disk.
- Open `Assets/Scenes/Game.unity` and press Play.
- The first time you open it on a new computer Unity imports everything; that can take a few minutes.

## Tools menu (Tools > I Am A Ball)

| Menu item | What it does |
| --- | --- |
| Build Everything (City + Gameplay) | Fills the city with buildings, props, shops, NPCs, police, traffic, HUD and minimap. Safe to run again; it replaces what it made before. |
| 1. Make Prefabs From Models | Makes a ready-to-use prefab (with a collider) for every model. |
| 2. Build City Details | Only the buildings, props and places. |
| 3. Set Up Gameplay | Only the systems, HUD, NPCs, police and traffic. |
| Take Screenshots | Saves pictures of the map to the `Screenshots` folder. |
| Run Test Simulation | Plays the game by itself, tests every system and writes `Screenshots/Simulation/results.txt`. |
| Remove Generated Stuff | Deletes everything the tools made. Your own objects are never touched. |

## Adding your own models

Export from Blender as **.fbx** and put the file in `Assets/Models/<Category>/`
(for example `Assets/Models/Buildings/my-house.fbx`). A prefab with a collider appears
automatically in `Assets/Prefabs/Auto/<Category>/`. Use .fbx instead of .blend so the
project also works on computers without Blender. The original .blend files of my older models
are kept in the `BlenderSources` folder.

## Working on two computers

1. Before you start: open GitHub Desktop and **Pull**.
2. When you finish: save the scene in Unity, close Unity, then **Commit** and **Push**.
3. Don't edit the same scene on both computers without pulling in between.

## Folders

| Folder | What is in it |
| --- | --- |
| `Assets/Scripts` | My original scripts (player, camera, money, collectibles, day/night) |
| `Assets/Scripts/Sim` | New gameplay: needs, shops, job, homes, police, NPCs, traffic, HUD, minimap |
| `Assets/Editor` | The Tools menu (city builder, gameplay setup, model auto setup, screenshots, test) |
| `Assets/ThirdParty/Kenney` | Low poly models and sounds by Kenney (CC0) |
| `Assets/Prefabs/Auto` | Prefabs made automatically from models |
| `Assets/Generated` | Materials and the animation controller made by the tools |

## Credits

- 3D models and sounds: [Kenney](https://kenney.nl) (CC0): City Kit Commercial, City Kit
  Suburban, City Kit Roads, Car Kit, Mini Characters, Food Kit, Nature Kit, Furniture Kit,
  UI Audio, Impact Sounds.
- Music by Kevin MacLeod (incompetech.com), licensed under Creative Commons: By Attribution 4.0
  (https://creativecommons.org/licenses/by/4.0/): "Easy Lemon", "Monkeys Spinning Monkeys",
  "Sneaky Snitch", "Movement Proposition".
- Gunshot: "gun_fire.wav" by AVW (https://opengameart.org/content/collection-gun-sounds), CC BY 3.0.
- Car engine, horn, siren and street sounds: CC0 sounds from OpenGameArt and Wikimedia Commons
  (full list in `Assets/Resources/Audio/AUDIO_CREDITS.txt`).
- My own models made in Blender: roads, road lights, tunnel, sewer entrance, player character.
- Other models in `Assets/prefab` (bank, ATM, park fountain, mountain, trash can): add where
  they came from here.
