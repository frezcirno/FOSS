#include "pch.h"
#include "GzipLib.h"
#include <iostream>
#include <sstream>
#include <fstream>
#include <boost/iostreams/filtering_streambuf.hpp>
#include <boost/iostreams/copy.hpp>
#include <boost/iostreams/filter/gzip.hpp>
#include <boost/filesystem/operations.hpp>
#include <msclr\marshal.h>
#include <msclr\marshal_cppstd.h>

	
System::String^ GzipLib::Class1::compress(System::String^ data)
{
	namespace bio = boost::iostreams;
	std::string unmanaged = msclr::interop::marshal_as<std::string>(data);
	std::stringstream origin(unmanaged);
	std::stringstream compressed;

	bio::filtering_streambuf<bio::input> out;
	out.push(bio::gzip_compressor(bio::gzip_params(bio::gzip::best_compression)));
	out.push(origin);
	bio::copy(out, compressed);

	return msclr::interop::marshal_as<System::String^>(compressed.str());
}

System::String^ GzipLib::Class1::decompress(System::String^ data)
{
	namespace bio = boost::iostreams;
	std::string unmanaged = msclr::interop::marshal_as<std::string>(data);
	std::stringstream origin(unmanaged);
	std::stringstream decompressed;

	bio::filtering_streambuf<bio::input> out;
	out.push(bio::gzip_decompressor());
	out.push(origin);
	bio::copy(out, decompressed);

	return msclr::interop::marshal_as<System::String^>(decompressed.str());
}
