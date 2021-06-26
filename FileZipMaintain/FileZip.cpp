// FileZipMaintain.cpp : 定义 DLL 应用程序的导出函数。
//

#include "stdafx.h"

#include "FileZip.h"

#include <Shlwapi.h>
#pragma comment(lib, "shlwapi.lib")

std::vector<std::string> files;

void InitOrRest()
{
	files.clear();
}

void __stdcall AddFile(char* search, char* path)
{
	std::string file = std::string(path) + std::string(search);
	files.push_back(file);
}

bool __stdcall CheckFile(char* fileName, char* path)
{
	std::string pathStr = std::string(path);
	if (pathStr.back() != '/')
		pathStr += '/';
	std::string file = pathStr + std::string(fileName);
	if (PathFileExists(file.c_str()) == 1)
		return true;
	else
		return false;
}

void __stdcall CopyToDir(char* path, char* dirName)
{
	// 检查路径合法性
	std::string pathStr = std::string(path);
	std::string dirNameStr = std::string(dirName);
	if (pathStr.back() != '/' && dirNameStr[0] != '/')
		pathStr += '/';

	// 尝试创建文件夹并清空
	std::string completeDir = pathStr + dirNameStr;
	try {
		CreateDirectory(completeDir.c_str(), NULL);
	}
	catch (...) {}
	// 用于查找的句柄
	long handle;
	struct _finddata_t fileinfo;
	if (completeDir.back() != '/')
		completeDir += '/';
	std::string inPath = completeDir + '*';
	//第一次查找
	handle = _findfirst(inPath.c_str(), &fileinfo);
	if (handle != -1)
	{
		do
		{
			if (strcmp(fileinfo.name, ".") != 0 && strcmp(fileinfo.name, "..") != 0)
			{
				std::string filePath = completeDir + '/' + std::string(fileinfo.name);
				remove(filePath.c_str());
			}
		} while (!_findnext(handle, &fileinfo));
	}
	_findclose(handle);

	// 复制文件到文件夹中
	for (unsigned i = 0; i < files.size(); i++)
	{
		std::string completeFile = files.at(i);
		size_t beginPos = completeFile.find('/', 2);
		std::string fileName = completeFile.substr(beginPos + 1, completeFile.length() - beginPos - 1);
		std::ifstream ifs(completeFile, std::ios::binary);
		std::ofstream ofs(completeDir + fileName, std::ios::binary);
		if (!ifs.is_open() || !ofs.is_open())
			continue;
		ofs << ifs.rdbuf();
		ifs.close();
		ofs.close();
	}

	// 重置文件容器
	InitOrRest();
}

void __stdcall RemoveDir(char* path, char* dirName)
{
	// 检查路径合法性
	std::string pathStr = std::string(path);
	std::string dirNameStr = std::string(dirName);
	if (pathStr.back() != '/' && dirNameStr[0] != '/')
		pathStr += '/';

	// 删除文件夹，先删除其中的文件
	std::string completeDir = pathStr + dirNameStr;
	// 用于查找的句柄
	long handle;
	struct _finddata_t fileinfo;
	std::string inPath = completeDir + "/*";
	//第一次查找
	handle = _findfirst(inPath.c_str(), &fileinfo);
	if (handle == -1)
	{
		RemoveDirectory(completeDir.c_str());
		return;
	}
	do
	{
		if (strcmp(fileinfo.name, ".") != 0 && strcmp(fileinfo.name, "..") != 0)
		{
			std::string filePath = completeDir + '/' + std::string(fileinfo.name);
			remove(filePath.c_str());
		}
	} while (!_findnext(handle, &fileinfo));
	_findclose(handle);
	RemoveDirectory(completeDir.c_str());
}