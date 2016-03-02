#include "pch.h"
#include "CodeParser.h"

#define BLOCK_WAIT_TIME 100

using namespace CodeParser;

struct text_block
{
  std::wstring text;
  uint32_t pos;
  bool clear;

  text_block(wchar_t const *data, int32_t len, int32_t pos,
             bool clear = false)
    : text(data, len)
    , pos(pos)
    , clear(clear)
  {}
};

enum class pstate : wchar_t
{
  unknown,
};

class c_parser
{
  std::vector<std::pair<wchar_t, pstate>> &text;
  std::map<std::wstring, std::wstring> macros;
public:
  c_parser(std::vector<std::pair<wchar_t, pstate>> &text)
    : text(text)
  {}

  c_parser(c_parser const &) = delete;
  c_parser(c_parser &&) = delete;

  ~c_parser()
  {}

  c_parser &operator=(c_parser const &) = delete;
  c_parser &operator=(c_parser &&) = delete;

  void parse(std::function<void(int32_t begin, int32_t end,
                                CodeParser::TokenType type)> report)
  {
    auto const len = static_cast<signed>(text.size());
    int i = -1;
#define CHECK i < len
    int start;
    report(0, len, CodeParser::TokenType::None);
    while (++CHECK)
      switch (text[i].first)
      {
      case L'#':
        start = i;
       lex_pp_keyword:if (++CHECK)
          switch (text[i].first)
          {
          case L'd':
            if ((++CHECK) && text[i].first == L'e' &&
                (++CHECK) && text[i].first == L'f' &&
                (++CHECK) && text[i].first == L'i' &&
                (++CHECK) && text[i].first == L'n' &&
                (++CHECK) && text[i].first == L'e')
              {
                report(start, i + 1, CodeParser::TokenType::Directive);
                while ((++CHECK) && text[i].first == L' ')
                  ;
                if (!(CHECK))
                  break;
                std::wstring macro_name;
                if (std::iswalpha(text[i].first))
                  {
                    start = i;
                    macro_name.push_back(text[i].first);
                    while ((++CHECK) && std::iswalnum(text[i].first))
                      macro_name.push_back(text[i].first);
                    report(start, i, CodeParser::TokenType::Macro);
                    while (text[i].first == L' ' && (++CHECK))
                      ;
                    std::wstring macro_value;
                    while (text[i].first != L'\n' && (++CHECK))
                      macro_value.push_back(text[i].first);
                    this->macros[macro_name] = macro_value;
                  }
              }
            break;
          case L' ':
            goto lex_pp_keyword;
          default:
            report(start, i, CodeParser::TokenType::Error);
          }
        break;
      default:
        auto const cchar = text[i].first;
        if (std::iswalpha(cchar))
          {
            start = i;
          }
      }
#undef CHECK
  }
};

void AsyncParser::looper(void)
{
  auto reporter = [this](int32_t begin, int32_t end,
                         CodeParser::TokenType type) {
    this->NewTokenParsed({ begin, end, type });
  };
  struct text_block *block;
  std::vector<std::pair<wchar_t, pstate>> text;
  c_parser parser(text);
  while (this->running)
    {
      while (!this->new_blocks.try_pop(block))
        ::WaitForSingleObjectEx(::GetCurrentThread(), BLOCK_WAIT_TIME, FALSE);
      if (block->clear)
        {
          text.clear();
          delete block;
          continue;
        }
      for (auto wchar : block->text)
        text.emplace_back(std::make_pair(wchar, pstate::unknown));
      parser.parse(reporter);
      delete block;
    }
}

AsyncParser::AsyncParser(pfm::String ^text)
  : new_blocks()
  , running(true)
  , looper_thread([this] { this->looper(); })
{}

AsyncParser::~AsyncParser()
{
  text_block *block;
  while (this->new_blocks.try_pop(block))
    delete block;
}

void AsyncParser::NewTextBlock(pfm::String ^text, int32 pos)
{
  this->new_blocks.push(new text_block(text->Data(), text->Length(), pos));
}

void AsyncParser::Clear(void)
{
  this->new_blocks.clear();
  this->new_blocks.push(new text_block(nullptr, 0, 0, true));
}
