// dllmain.cpp: DllMain 的实现。

#include "pch.h"
#include "framework.h"
#include "resource.h"
#include "MD5Lib_i.h"
#include "dllmain.h"

CMD5LibModule _AtlModule;

// DLL 入口点
extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
	hInstance;
	return _AtlModule.DllMain(dwReason, lpReserved);
}


STDMETHODIMP CMD5LibModule::digest(BSTR data, BSTR* result)
{
	// TODO: 在此处添加实现代码

	return S_OK;
}
