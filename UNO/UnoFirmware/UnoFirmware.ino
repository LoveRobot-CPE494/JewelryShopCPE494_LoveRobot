#include "src/CommandParser.h"

constexpr unsigned long kUnoBaudRate = 115200;

void setup() { Serial.begin(kUnoBaudRate); }

// shortcut: empty loop; the Serial protocol wiring to parseCommand is a later ticket.
void loop() {}
