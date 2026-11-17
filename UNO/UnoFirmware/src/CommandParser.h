#ifndef UNO_FIRMWARE_COMMAND_PARSER_H_
#define UNO_FIRMWARE_COMMAND_PARSER_H_

enum class Command { kUnknown, kStart, kStop };

// Parses one command line (no newline) into a Command; kUnknown if not recognised.
Command parseCommand(const char* line);

#endif  // UNO_FIRMWARE_COMMAND_PARSER_H_
