#define DOCTEST_CONFIG_IMPLEMENT_WITH_MAIN
#include "../UnoFirmware/src/CommandParser.h"
#include "vendor/doctest.h"

TEST_CASE("parses known commands and rejects anything else") {
  CHECK(parseCommand("START") == Command::kStart);
  CHECK(parseCommand("STOP") == Command::kStop);
  CHECK(parseCommand("start") == Command::kUnknown);
  CHECK(parseCommand("") == Command::kUnknown);
  CHECK(parseCommand(nullptr) == Command::kUnknown);
}
