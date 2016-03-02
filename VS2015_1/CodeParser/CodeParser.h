#pragma once

namespace pfm = ::Platform;
namespace cnc = ::Concurrency;

struct text_block;

namespace CodeParser
{
  public enum class TokenType
  {
    None,
    Error,
    Directive,
    Macro,
  };

  public value struct Token sealed
  {
    int32 Begin;
    int32 End;
    TokenType Type;
  };

  public delegate void NewTokenParsed(Token token);

  public ref class AsyncParser sealed
  {
    cnc::concurrent_queue<text_block *> new_blocks;
    bool running;
    std::thread looper_thread;
    void looper(void);
    ~AsyncParser();
  public:
    AsyncParser(pfm::String ^baseText);
    event NewTokenParsed ^NewTokenParsed;
    void Clear(void);
    void NewTextBlock(pfm::String ^textBlock, int32 position);
  };
}
