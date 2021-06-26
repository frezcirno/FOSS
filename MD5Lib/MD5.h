// MD5.h: CMD5 的声明

#pragma once
#include "resource.h"       // 主符号
#include "pch.h"
#include "MD5Lib_i.h"

#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Windows CE 平台(如不提供完全 DCOM 支持的 Windows Mobile 平台)上无法正确支持单线程 COM 对象。定义 _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA 可强制 ATL 支持创建单线程 COM 对象实现并允许使用其单线程 COM 对象实现。rgs 文件中的线程模型已被设置为“Free”，原因是该模型是非 DCOM Windows CE 平台支持的唯一线程模型。"
#endif

using namespace ATL;


// CMD5
#define uint8  unsigned char
#define uint32 unsigned long int

int _httoi(const char* value);

class ATL_NO_VTABLE CMD5 :
	public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<CMD5, &CLSID_MD5>,
	public IDispatchImpl<IMD5, &IID_IMD5, &LIBID_MD5LibLib, /*wMajor =*/ 1, /*wMinor =*/ 0>
{
public:
	CMD5()
	{
		for (int i = 0; i < 4; i++)
			m_data[i] = 0;
	}

	DECLARE_REGISTRY_RESOURCEID(106)


	BEGIN_COM_MAP(CMD5)
		COM_INTERFACE_ENTRY(IMD5)
		COM_INTERFACE_ENTRY(IDispatch)
	END_COM_MAP()



	DECLARE_PROTECT_FINAL_CONSTRUCT()

	HRESULT FinalConstruct()
	{
		return S_OK;
	}

	void FinalRelease()
	{
	}

private:

	struct md5_context
	{
		uint32   total[2];
		uint32 state[4];
		uint8 buffer[64];
	};
public:

	void md5_starts(md5_context* ctx);

	void md5_process(md5_context* ctx, uint8 data[64]);

	void md5_update(md5_context* ctx, uint8* input, uint32 length);

	void md5_finish(md5_context* ctx, uint8 digest[16]);

	void GenerateMD5(unsigned char* buffer, int bufferlen);

	unsigned long m_data[4];

	CMD5(unsigned long* md5src) {
		memcpy(m_data, md5src, 16);
	}

	CMD5(const char* md5src) {
		if (strcmp(md5src, "") == 0)
		{
			for (int i = 0; i < 4; i++)
				m_data[i] = 0;
			return;
		}
		for (int j = 0; j < 16; j++)
		{
			char buf[10];
			strncpy_s(buf, md5src, 2);
			md5src += 2;
			((unsigned char*)m_data)[j] = _httoi(buf);
		}
	}

	std::string ToString() {
		char output[33];
		for (int j = 0; j < 16; j++)
		{
			sprintf(output + j * 2, "%02x", ((unsigned char*)m_data)[j]);
		}
		return std::string(output);
	}

	STDMETHOD(digest)(BSTR data, BSTR* result);
};

OBJECT_ENTRY_AUTO(__uuidof(MD5), CMD5)
