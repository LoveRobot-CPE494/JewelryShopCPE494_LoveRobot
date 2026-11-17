#include "CommandParser.h"

#include <string.h>

Command parseCommand(const char* line) {
  if (line == nullptr) return Command::kUnknown;
  if (strcmp(line, "START") == 0) return Command::kStart;
  if (strcmp(line, "STOP") == 0) return Command::kStop;
  return Command::kUnknown;
}
