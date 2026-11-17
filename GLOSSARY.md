# JewelryShop

A virtual jewelry store on Meta Quest 3, paired with an automated gift-packing station (MG400 + conveyor), per the CPE494 brief.

## Language

### Storefront

**Storefront**:
The jewelry store in the virtual world where a Customer browses and buys through Meta Quest 3
_Avoid_: AR shop, shop scene, AR storefront

**Controller**:
The single right-hand controller that ships with the team's Quest 3 (there is no left Controller), used as the "hand" of both the Customer and the Maintenance Operator
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
The list of Gems (each with its Cut + Color) that a Customer pays for in one payment. Every Gem in an Order goes into the same Gift Bag
_Avoid_: purchase, cart

**Checkout**:
The spot in the Storefront where the Customer taps a card on a virtual terminal to pay
_Avoid_: payment area, cashier

**Staff**:
The store's sales assistant whom the Customer meets in the Storefront. Does not control the robot arm
_Avoid_: salesperson, employee (ambiguous with Maintenance Operator)

**Staff Call**:
A Customer's request for Staff to come and talk with them in the Storefront
_Avoid_: call button, help

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
_Avoid_: storage, inventory, tray

**Stock Slot**:
One position in the Gem Stock for a single Cut + Color. It carries a label people can read and can hold several Gem Pieces
_Avoid_: bin, cell, compartment

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
The intermediary that receives Orders and Teleop commands from the Quest, then commands the MG400 and the Arduino
_Avoid_: server, orchestrator, backend

### Remote maintenance

**Maintenance Operator**:
The staff member who wears a Quest and drives the robot arm remotely to retrieve a Dropped Gem
_Avoid_: technician, admin, driver

**Teleop**:
The mode in which the Maintenance Operator's Controller position is mapped to the MG400 end-effector position
_Avoid_: remote control, manual mode

**Dropped Gem**:
A Gem Piece lying on the MG400's table or on the Gem Stock but not in any Stock Slot
_Avoid_: lost gem, fallen item

**Deadman**:
The press-and-hold the Maintenance Operator must keep up for the arm to follow Teleop. Release it and the arm stops immediately
_Avoid_: enable button, safety button
