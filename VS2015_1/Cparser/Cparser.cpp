#include "pch.h"
#include "Cparser.h"

using namespace Cparser;

CParser::CParser(Pfm::String ^text)
  : text(text->Length())
{
  auto begin = text->Begin() - sizeof(decltype(*text->Begin()));
  for (auto i = 0U; i < text->Length(); ++i)
    this->text[i] = std::make_pair(*++begin, 0);
  void(text->Data());
}

CParser::~CParser()
{
}
