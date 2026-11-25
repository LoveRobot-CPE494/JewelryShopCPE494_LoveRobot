# JewelryShop

A virtual jewelry store on Meta Quest 3, paired with an automated gift-packing station (MG400 + conveyor), per the CPE494 brief.

## Language

### Storefront

**Storefront**:
The jewelry store in the virtual world where a Customer browses and buys through Meta Quest 3
_Avoid_: AR shop, shop scene, AR storefront

**Controller**:
The Quest 3's hand controllers, left and right (the team first thought it had only a right one), used as the "hands" of the Customer. The right Controller is also the Maintenance Operator's hand that drives the arm in Teleop; the left plays no part in driving the arm, but opens the Robot Panel in AR Mode ([ADR-0021](Docs/adr/0021-two-controllers-in-the-storefront.md), [ADR-0026](Docs/adr/0026-teleop-controls-engage-toggle-pump-hand-menus.md))
_Avoid_: gripper (that word means the arm's end effector, see Suction Cup), joystick, hand

**Customer**:
The person who wears the Quest into the Storefront to browse and buy Gems
_Avoid_: buyer, user

**Gem**:
One gem, identified by its Cut and Color
_Avoid_: jewel, jewelry, stone, item

**Cut**:
The shape of a Gem. There are only two: Round and Emerald
_Avoid_: shape, type

**Color**:
The color of a Gem. The colors that really exist depend on the Gem Pieces available
_Avoid_: colour, variant

**Display Case**:
The case in the Storefront holding Gem Models of mixed Cut and Color for the Customer to pick up and examine
_Avoid_: showcase, shelf

**Gem Model**:
The 3D model of a Gem in the Display Case. It is a catalog entry: buying it does not remove it from the case
_Avoid_: virtual gem, 3D gem, display item

**Order**:
The list of Gems (each with its Cut + Color, repeats allowed) that a Customer pays for in one payment. Every Gem in an Order goes into the same Gift Bag
_Avoid_: purchase, cart

**Order Tray**:
The tray on the Checkout Counter that holds the Gems the Customer has added to the Order
_Avoid_: basket, cart, drop zone

**Gem Details**:
The catalog facts of one Cut + Color, such as its name, carat and clarity, shown to the Customer
_Avoid_: gem info, spec sheet, stats

**Gem Details Panel**:
The floating panel beside a held Gem Model that shows its Gem Details
_Avoid_: info card, card (that is the Payment Card), popup, tooltip

**Checkout**:
The step where the Customer pays for an Order by tapping the Payment Card on the Card Terminal. It is approved only once the Hub has accepted the Order
_Avoid_: payment, paying

**Checkout Counter**:
The spot in the Storefront where the Checkout takes place
_Avoid_: payment area, cashier

**Price**:
The fixed amount in baht of one Gem of a given Cut + Color. An Order's total is the sum of the Prices of its Gems
_Avoid_: cost, value

**Payment Card**:
The virtual card on the Checkout Counter that the Customer picks up and taps to pay. It is never declined for its amount
_Avoid_: credit card, card (alone), wallet

**Card Terminal**:
The virtual device on the Checkout Counter that shows the Order's total and takes the tap of the Payment Card
_Avoid_: POS, reader, payment machine, EDC

**Receipt**:
The slip the Card Terminal gives the Customer after an approved Checkout, listing the Order's Gems, their Prices and its total
_Avoid_: bill, invoice, slip

**Staff**:
The real person who serves the Customer live from the Staff Console, appearing in the Storefront as the Staff Avatar. Does not control the robot arm
_Avoid_: salesperson, employee (ambiguous with Maintenance Operator)

**Staff Avatar**:
The 3D figure in the Storefront that the Staff speaks and acts through
_Avoid_: NPC, bot, character

**Staff Console**:
The Staff's workstation outside the Storefront, from which they serve a Customer live
_Avoid_: staff app, admin panel, dashboard

**Idle Lock**:
A Staff Console toggle (key 7, OFF at every start) that keeps the Staff Avatar in the plain standing pose instead of playing random Standby fidgets
_Avoid_: freeze, pause pose

**Expression**:
A facial look (a VRM preset such as happy, relaxed or sad) the Staff Avatar shows with each gesture, then holds briefly and fades. Without a gesture it keeps a light smile, and the same driver blinks on its own in every state. See [ADR-0019](Docs/adr/0019-staff-avatar-gesture-expressions.md)
_Avoid_: emote, face animation, blendshape (that is the mesh mechanism, not the term)

**Look-At**:
A Staff Console toggle (key 8, OFF by default, remembered between runs) that makes the Staff Avatar's head and eyes follow a target within fixed angle limits. See [ADR-0020](Docs/adr/0020-staff-avatar-look-at.md)
_Avoid_: gaze, head tracking

**Focus**:
A Staff Console toggle (key F, OFF at every start) that aims at a nearby Customer Avatar or an Interest Target. It operates independently of key 8. See [ADR-0020](Docs/adr/0020-staff-avatar-look-at.md)
_Avoid_: lock-on, glance

**Reset Look**:
A Staff Console one-shot command (key R) that steps the Staff Avatar back toward the Staff Console camera heading. The same turn plays after a gesture. See [ADR-0020](Docs/adr/0020-staff-avatar-look-at.md)
_Avoid_: recenter, reset pose

**Interest Target**:
An object Focus can aim at. Assign it in the Inspector or aim the camera at a collidable surface and press F. See [ADR-0020](Docs/adr/0020-staff-avatar-look-at.md)
_Avoid_: look target, aim point

**Staff Config**:
A data-only ScriptableObject asset holding the Staff Console's tuning values (movement or camera), with plain-C# validation in the Core assembly. See [ADR-0018](Docs/adr/0018-staff-config-data-only-scriptable-objects.md)
_Avoid_: settings file, event channel

**Staff Room**:
The back room behind the Storefront, where the Staff Avatar waits while no Staff Call is being served. The Customer can see in from the Storefront. See [ADR-0022](Docs/adr/0022-cheryll-exists-from-link-connect.md)
_Avoid_: back office, backstage

**Staff Door**:
The door between the Storefront and the Staff Room
_Avoid_: back door

**Front Door**:
The glass door between the Storefront and the Street. Like the Staff Door, it opens by itself for the Customer or the Staff Avatar and closes again behind them
_Avoid_: entrance, main door

**Street**:
The one block of road outside the Front Door, where the Customer and the Staff Avatar may walk freely
_Avoid_: outside, city, exterior

**Decor**:
Everything in the Storefront, the Staff Room and the Street that is only there to look at and is never picked up. Decorative jewelry is never a Gem Model and stays out of the Customer's reach
_Avoid_: props, decoration

**Customer Avatar**:
The 3D figure that stands in for the Customer on the Staff Console, seen only by the Staff
_Avoid_: player model, buyer avatar

**Staff Call**:
A Customer's request for the Staff to come and serve them live in the Storefront
_Avoid_: call button, help

**Host Address**:
The LAN address of the Staff Console's PC that the Quest's Staff Link connects to, typed in the headset or found by Host Discovery. Teleop uses the Robot Address instead. See [ADR-0023](Docs/adr/0023-lan-host-address-and-udp-discovery.md)
_Avoid_: server IP, PC IP

**Host Discovery**:
The UDP broadcast in the LAN that lets the Quest find the Host Address (from the Staff Console) and the Robot Address (from the Robot PC) without typing them. Each beacon carries its role, `staff` or `robot`. See [ADR-0023](Docs/adr/0023-lan-host-address-and-udp-discovery.md)
_Avoid_: auto IP, pairing

**Host Beacon**:
One Host Discovery packet: its sender's role (`staff` or `robot`), IPv4 address and port (Staff Link or Teleop), broadcast about once a second. See [ADR-0023](Docs/adr/0023-lan-host-address-and-udp-discovery.md)
_Avoid_: ping, announce packet

**Incoming**:
The Staff Console state while a Staff Call rings and has not been answered yet

**Serving**:
The Staff Console state from answering a Staff Call until it ends, while Cheryll is out serving the Customer

**In call**:
The Customer state once Cheryll has reached them, until the Staff Call ends

### Packing station

**Packing Station**:
All of the real hardware: the MG400, conveyor, Gem Stock and Bag Closer
_Avoid_: cell, robot station

**Packing Sequence**:
All the packing work for one Order on the Packing Station, from the Gift Bag arriving in front of the arm until it leaves at the end of the conveyor
_Avoid_: packing flow, process

**Gem Piece**:
A real miniature gem on the Packing Station, with a real Cut and Color
_Avoid_: real gem, block, cube

**Gem Stock**:
The storage for Gem Pieces on the Packing Station, made up of several Stock Slots
_Avoid_: storage, inventory, tray (that is the Order Tray)

**Stock Slot**:
One position in the Gem Stock for a single Cut + Color. It carries a label people can read and can hold several Gem Pieces
_Avoid_: bin, cell, compartment

**Stock Count**:
The number of Gem Pieces of one Cut + Color that the Staff say are in its Stock Slot. It is entered by people, not measured
_Avoid_: quantity, stock level

**Gift Bag**:
The paper bag on the conveyor. One bag receives every Gem Piece of one Order
_Avoid_: gift box, box

**Suction Cup**:
The suction head at the end of the MG400 arm, used to pick up Gem Pieces
_Avoid_: gripper

**Bag Closer**:
The mechanism that closes the mouth of the Gift Bag
_Avoid_: lid servo, gripper, lid-closing hand

**Hub**:
The intermediary that receives Orders from the Quest, then commands the MG400 and the Arduino. Teleop goes to the Teleop Bridge instead
_Avoid_: server, orchestrator, backend

### Remote maintenance

**Maintenance Operator**:
The team member who wears a Quest and drives the robot arm remotely to retrieve a Dropped Gem
_Avoid_: technician, admin, driver

**Teleop**:
The mode in which the Maintenance Operator's Controller position is mapped to the MG400 end-effector position
_Avoid_: remote control, manual mode

**Teleop Bridge**:
The Robot PC program that receives Teleop and Robot Panel messages from the Quest over UDP, clamps them to the safe workspace, drives the MG400 and sends Robot Status back (`Dobot/teleop/`, moving to `Dobot/src/hub` in #216; [ADR-0024](Docs/adr/0024-robot-panel-presets-and-robot-status.md))
_Avoid_: relay, teleop server

**Robot PC**:
The PC wired to the MG400, running the Teleop Bridge. It is not the Staff Console's PC, but shares its LAN ([ADR-0024](Docs/adr/0024-robot-panel-presets-and-robot-status.md))
_Avoid_: Dobot PC, robot server

**Robot Address**:
The LAN address of the Robot PC that the Quest sends Teleop and Robot Panel commands to, typed in the headset or found from the Robot PC's beacon. See [ADR-0023](Docs/adr/0023-lan-host-address-and-udp-discovery.md)
_Avoid_: Dobot IP, MG400 IP (that is the arm's own address, set only on the Robot PC)

**Robot Panel**:
The left Hand Menu in AR Mode, whose slots send commands and Preset Actions to the Teleop Bridge ([ADR-0024](Docs/adr/0024-robot-panel-presets-and-robot-status.md), [ADR-0026](Docs/adr/0026-teleop-controls-engage-toggle-pump-hand-menus.md))
_Avoid_: control page, dashboard (that is the MG400's port 29999), robot UI

**Preset Action**:
A named, numbered list of arm steps (move, suction, wait, home) kept in a file on the Robot PC, checked against the safe workspace when loaded and run by the Teleop Bridge when the Robot Panel sends its id. "Preset" alone means the same ([ADR-0024](Docs/adr/0024-robot-panel-presets-and-robot-status.md))
_Avoid_: macro, script, routine

**Robot Status**:
The packet the Teleop Bridge sends back to the Quest a few times a second: robot mode, error, pose, pump state, Stop latch, last command result, the Preset list and the running Preset's progress ([ADR-0024](Docs/adr/0024-robot-panel-presets-and-robot-status.md))
_Avoid_: feedback (that is the MG400's port 30004), telemetry, ack

**Dropped Gem**:
A Gem Piece lying on the MG400's table or on the Gem Stock but not in any Stock Slot
_Avoid_: lost gem, fallen item

**Engage**:
The state in which the arm follows the Maintenance Operator's right Controller in AR Mode ([ADR-0026](Docs/adr/0026-teleop-controls-engage-toggle-pump-hand-menus.md))
_Avoid_: Deadman (the retired press-and-hold), enable (that is the MG400's Enable), control mode

**Hand Menu**:
A radial menu at one Controller. The right one switches between AR Mode and Game Mode; the left one, in AR Mode only, is the Robot Panel ([ADR-0026](Docs/adr/0026-teleop-controls-engage-toggle-pump-hand-menus.md))
_Avoid_: action menu, pie menu, wheel, wrist panel

**AR Mode**:
The headset showing the real room through its cameras, where the Maintenance Operator drives the arm in Teleop. Not the Storefront ([ADR-0026](Docs/adr/0026-teleop-controls-engage-toggle-pump-hand-menus.md))
_Avoid_: AR shop, AR storefront, maintenance scene

**Game Mode**:
The headset showing the Storefront to a Customer; "Game" is its label in the Hand Menu ([ADR-0026](Docs/adr/0026-teleop-controls-engage-toggle-pump-hand-menus.md))
_Avoid_: VR mode, shop mode
