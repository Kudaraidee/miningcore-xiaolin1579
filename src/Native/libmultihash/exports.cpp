/*
Copyright 2017 Coin Foundry (coinfoundry.org)
Authors: Oliver Weichhold (oliver@weichhold.com)
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#include "allium.h"
#include "aurum.h"
#include "bcrypt.h"
#include "keccak.h"
#include "quark.h"
#include "scryptjane.h"
#include "scryptn.h"
#include "neoscrypt.h"
#include "skein.h"
#include "skein2.h"
#include "x11.h"
#include "groestl.h"
#include "blake.h"
#include "fugue.h"
#include "geek.h"
#include "qubit.h"
#include "s3.h"
#include "verthash/tiny_sha3/sha3.h"
#include "hefty1.h"
#include "shavite3.h"
#include "x13.h"
#include "x14.h"
#include "minotaurx.h"
#include "nist5.h"
#include "x15.h"
#include "x17.h"
#include "x22i.h"
#include "fresh.h"
#include "dcrypt.h"
#include "jh.h"
#include "c11.h"
#include "Lyra2RE.h"
#include "Lyra2.h"
#include "x16r.h"
#include "x16rt.h"
#include "x16s.h"
#include "x16rv2.h"
#include "x21s.h"
#include "sha256csm.h"
#include "sha512_256.h"
#include "sha256dt.h"
#include "hmq17.h"
#include "phi.h"
#include "verthash/h2.h"
#include "equi/equihashverify.h"
#include "heavyhash/heavyhash.h"
#include "skydoge.h"
#include "yescrypt/yescrypt.h"
#include "yespower/yespower.h"
#include "memehash.h"
#include "rwahash.h"
#include "x20r.h"
#include "x11gost.h"

#ifdef _WIN32
#include "blake2/ref/blake2.h"
#else
#include "blake2/sse/blake2.h"
#endif

#include "blake3/blake3.h"

#ifdef _WIN32
#define MODULE_API __declspec(dllexport)
#else
#define MODULE_API
#endif

extern "C" MODULE_API void scrypt_export(const char *input, char *output, uint32_t N, uint32_t R, uint32_t input_len)
{
	scrypt_N_R_1_256(input, output, N, R, input_len);
}

extern "C" MODULE_API void quark_export(const char *input, char *output, uint32_t input_len)
{
	quark_hash(input, output, input_len);
}

extern "C" MODULE_API void sha256csm_export(const char *input, char *output, uint32_t input_len)
{
	sha256csm_hash(input, output, input_len);
}

extern "C" MODULE_API void sha3_256_export(const char *input, char *output, uint32_t input_len)
{
	sha3(input, input_len, output, 32);
}

extern "C" MODULE_API void sha3_512_export(const char *input, char *output, uint32_t input_len)
{
	sha3(input, input_len, output, 64);
}

extern "C" MODULE_API void hmq17_export(const char *input, char *output, uint32_t input_len)
{
	hmq17_hash(input, output, input_len);
}

extern "C" MODULE_API void phi_export(const char *input, char *output, uint32_t input_len)
{
	phi_hash(input, output, input_len);
}

extern "C" MODULE_API void x11_export(const char *input, char *output, uint32_t input_len)
{
	x11_hash(input, output, input_len);
}

extern "C" MODULE_API void x13_export(const char *input, char *output, uint32_t input_len)
{
	x13_hash(input, output, input_len);
}

extern "C" MODULE_API void x13_bcd_export(const char *input, char *output)
{
	x13_bcd_hash(input, output);
}

extern "C" MODULE_API void x17_export(const char *input, char *output, uint32_t input_len)
{
	x17_hash(input, output, input_len);
}

extern "C" MODULE_API void x15_export(const char *input, char *output, uint32_t input_len)
{
	x15_hash(input, output, input_len);
}

extern "C" MODULE_API void neoscrypt_export(const unsigned char *input, unsigned char *output, uint32_t profile)
{
	neoscrypt(input, output, profile);
}

extern "C" MODULE_API void scryptn_export(const char *input, char *output, uint32_t nFactor, uint32_t input_len)
{
	unsigned int N = 1 << nFactor;

	scrypt_N_R_1_256(input, output, N, 1, input_len); // hardcode for now to R=1 for now
}

extern "C" MODULE_API void kezzak_export(const char *input, char *output, uint32_t input_len)
{
	keccak_hash(input, output, input_len);
}

extern "C" MODULE_API void bcrypt_export(const char *input, char *output, uint32_t input_len)
{
	bcrypt_hash(input, output);
}

extern "C" MODULE_API void skein_export(const char *input, char *output, uint32_t input_len)
{
	skein_hash(input, output, input_len);
}

extern "C" MODULE_API void skein2_export(const char *input, char *output, uint32_t input_len)
{
	skein2_hash(input, output, input_len);
}

extern "C" MODULE_API void groestl_export(const char *input, char *output, uint32_t input_len)
{
	groestl_hash(input, output, input_len);
}

extern "C" MODULE_API void groestl_myriad_export(const char *input, char *output, uint32_t input_len)
{
	groestlmyriad_hash(input, output, input_len);
}

extern "C" MODULE_API void blake_export(const char *input, char *output, uint32_t input_len)
{
	blake_hash(input, output, input_len);
}

extern "C" MODULE_API void blake2s_export(const char *input, char *output, uint32_t input_len, uint32_t output_len)
{
	blake2s(output, output_len == -1 ? BLAKE2S_OUTBYTES : output_len, input, input_len, NULL, 0);
}

extern "C" MODULE_API void blake2b_export(const char *input, char *output, uint32_t input_len, uint32_t output_len)
{
	blake2b(output, output_len == -1 ? BLAKE2B_OUTBYTES : output_len, input, input_len, NULL, 0);
}

extern "C" MODULE_API void blake3_export(const char* input, char* output, uint32_t input_length)
{
    blake3_hash(input, output, input_length);
}

extern "C" MODULE_API void dcrypt_export(const char *input, char *output, uint32_t input_len)
{
	dcrypt_hash(input, output, input_len);
}

extern "C" MODULE_API void fugue_export(const char *input, char *output, uint32_t input_len)
{
	fugue_hash(input, output, input_len);
}

extern "C" MODULE_API void geek_export(const char *input, char *output, uint32_t input_len)
{
	geek_hash(input, output, input_len);
}

extern "C" MODULE_API void qubit_export(const char *input, char *output, uint32_t input_len)
{
	qubit_hash(input, output, input_len);
}

extern "C" MODULE_API void s3_export(const char *input, char *output, uint32_t input_len)
{
	s3_hash(input, output, input_len);
}

extern "C" MODULE_API void hefty1_export(const char *input, char *output, uint32_t input_len)
{
	hefty1_hash(input, output, input_len);
}

extern "C" MODULE_API void shavite3_export(const char *input, char *output, uint32_t input_len)
{
	shavite3_hash(input, output, input_len);
}

extern "C" MODULE_API void nist5_export(const char *input, char *output, uint32_t input_len)
{
	nist5_hash(input, output, input_len);
}

extern "C" MODULE_API void fresh_export(const char *input, char *output, uint32_t input_len)
{
	fresh_hash(input, output, input_len);
}

extern "C" MODULE_API void jh_export(const char *input, char *output, uint32_t input_len)
{
	jh_hash(input, output, input_len);
}

extern "C" MODULE_API void c11_export(const char *input, char *output)
{
	c11_hash(input, output);
}

extern "C" MODULE_API void lyra2re_export(const char *input, char *output)
{
	lyra2re_hash(input, output);
}

extern "C" MODULE_API void lyra2rev2_export(const char *input, char *output)
{
	lyra2re2_hash(input, output);
}

extern "C" MODULE_API void lyra2rev3_export(const char *input, char *output)
{
	lyra2re3_hash(input, output);
}

extern "C" MODULE_API void x16r_export(const char *input, char *output, uint32_t input_len)
{
	x16r_hash(input, output, input_len);
}

extern "C" MODULE_API void x16rv2_export(const char *input, char *output, uint32_t input_len)
{
	x16rv2_hash(input, output, input_len);
}

extern "C" MODULE_API void x21s_export(const char *input, char *output, uint32_t input_len)
{
	x21s_hash(input, output, input_len);
}

extern "C" MODULE_API void x22i_export(const char *input, char *output, uint32_t input_len)
{
	x22i_hash(input, output, input_len);
}

extern "C" MODULE_API void sha512_256_export(const unsigned char *input, unsigned char *output, uint32_t input_len)
{
	sha512_256(input, input_len, output);
}

extern "C" MODULE_API void sha256dt_export(const char *input, char *output)
{
	sha256dt_hash(input, output);
}

extern "C" MODULE_API int verthash_init_export(const char *filename, int createIfMissing)
{
	return verthash_init(filename, createIfMissing);
}

extern "C" MODULE_API int verthash_export(const unsigned char *input, unsigned char *output, uint32_t input_len)
{
	return verthash(input, input_len, output);
}

extern "C" MODULE_API void x16s_export(const char *input, char *output, uint32_t input_len)
{
	x16s_hash(input, output, input_len);
}

extern "C" MODULE_API void heavyhash_export(const char *input, char *output, uint32_t input_len)
{
	heavyhash_hash(input, output, input_len);
}

extern "C" MODULE_API bool equihash_verify_200_9_export(const char *header, int header_length, const char *solution, int solution_length, const char *personalization)
{
	if (header_length != 140)
	{
		return false;
	}

	const std::vector<unsigned char> vecSolution(solution, solution + solution_length);

	return verifyEH_200_9(header, vecSolution, personalization);
}

extern "C" MODULE_API bool equihash_verify_144_5_export(const char *header, int header_length, const char *solution, int solution_length, const char *personalization)
{
	if (header_length != 140)
	{
		return false;
	}

	const std::vector<unsigned char> vecSolution(solution, solution + solution_length);

	return verifyEH_144_5(header, vecSolution, personalization);
}

extern "C" MODULE_API bool equihash_verify_96_5_export(const char *header, int header_length, const char *solution, int solution_length, const char *personalization)
{
	if (header_length != 140)
	{
		return false;
	}

	const std::vector<unsigned char> vecSolution(solution, solution + solution_length);

	return verifyEH_96_5(header, vecSolution, personalization);
}

extern "C" MODULE_API void skydoge_export(const char *input, char *output, uint32_t input_len)
{
	skydoge_hash(input, output, input_len);
}

extern "C" MODULE_API void yescrypt_export(const char *input, char *output, uint32_t input_len)
{
	yescrypt_hash(input, output, input_len);
}

extern "C" MODULE_API void yescryptR8_export(const char *input, char *output, uint32_t input_len)
{
	yescryptR8_hash(input, output, input_len);
}

extern "C" MODULE_API void yescryptR16_export(const char *input, char *output, uint32_t input_len)
{
	yescryptR16_hash(input, output, input_len);
}

extern "C" MODULE_API void yescryptR32_export(const char *input, char *output, uint32_t input_len)
{
	yescryptR32_hash(input, output, input_len);
}

extern "C" MODULE_API void allium_export(const char *input, char *output, uint32_t input_len)
{
	allium_hash(input, output, input_len);
}

extern "C" MODULE_API void aurum_export(const char *input, char *output, uint32_t input_len)
{
	aurum_hash(input, output, input_len);
}

extern "C" MODULE_API void x16rt_export(const char *input, char *output, uint32_t input_len)
{
	x16rt_hash(input, output, input_len);
}

extern "C" MODULE_API void power2b_export(const char *input, char *output, uint32_t input_len)
{
	power2b_hash(input, output, input_len);
}

extern "C" MODULE_API void cpupower_export(const char *input, char *output, uint32_t input_len)
{
	cpupower_hash(input, output, input_len);
}

extern "C" MODULE_API void yespower_export(const char *input, char *output, uint32_t input_len)
{
	yespower_hash(input, output, input_len);
}

extern "C" MODULE_API void yespowerR16_export(const char *input, char *output, uint32_t input_len)
{
	yespowerR16_hash(input, output, input_len);
}

extern "C" MODULE_API void yespowerIC_export(const char *input, char *output, uint32_t input_len)
{
	yespowerIC_hash(input, output, input_len);
}

extern "C" MODULE_API void yespowerSUGAR_export(const char *input, char *output, uint32_t input_len)
{
	yespowerSUGAR_hash(input, output, input_len);
}

extern "C" MODULE_API void yespowerTIDE_export(const char *input, char *output, uint32_t input_len)
{
	yespowerTIDE_hash(input, output, input_len);
}

extern "C" MODULE_API void yespowerURX_export(const char *input, char *output, uint32_t input_len)
{
	yespowerURX_hash(input, output, input_len);
}

extern "C" MODULE_API void minotaurx_export(const char *input, char *output, uint32_t input_len)
{
	minotaurx_hash(input, output, input_len);
}

extern "C" MODULE_API void yespowerLTNCG_export(const char *input, char *output, uint32_t input_len)
{
	yespowerLTNCG_hash(input, output, input_len);
}

extern "C" MODULE_API void memehash_export(const char *input, char *output, uint32_t input_len)
{
	meme_hash(input, output, input_len);
}

extern "C" MODULE_API void rwahash_export(const char *input, char *output, uint32_t input_len)
{
	rwahash_hash(input, output, input_len);
}

extern "C" MODULE_API void x20r_export(const char *input, char *output, uint32_t input_len)
{
	x20r_hash(input, output, input_len);
}

extern "C" MODULE_API void x11gost_export(const char *input, char *output, uint32_t input_len)
{
	x11gost_hash(input, output, input_len);
}
