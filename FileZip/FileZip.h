#pragma once

#include <iostream>
#include <vector>
#include <cstdio>
#include <fstream>
#include <io.h>
#include <string>

extern "C" _declspec(dllexport) void __stdcall AddFile(char* search, char* path);
extern "C" _declspec(dllexport) bool __stdcall CheckFile(char* fileName, char* path);
extern "C" _declspec(dllexport) void __stdcall CopyToDir(char* path, char* dirName);
extern "C" _declspec(dllexport) void __stdcall RemoveDir(char* path, char* dirName);
extern "C" _declspec(dllexport) void __stdcall Greeting(char* name);