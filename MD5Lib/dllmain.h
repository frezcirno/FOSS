// dllmain.h: 模块类的声明。

class CMD5LibModule : public ATL::CAtlDllModuleT< CMD5LibModule >
{
public :
	DECLARE_LIBID(LIBID_MD5LibLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_MD5LIB, "{15fc2e1b-2f10-4410-9202-6d835d1c3165}")
	STDMETHOD(digest)(BSTR data, BSTR* result);
};

extern class CMD5LibModule _AtlModule;
