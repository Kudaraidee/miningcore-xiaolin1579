#!/bin/bash

OutDir=$1

AES=$(../Native/check_cpu.sh aes && echo -maes || echo)
SSE2=$(../Native/check_cpu.sh sse2 && echo -msse2 || echo)
SSE3=$(../Native/check_cpu.sh sse3 && echo -msse3 || echo)
SSSE3=$(../Native/check_cpu.sh ssse3 && echo -mssse3 || echo)
PCLMUL=$(../Native/check_cpu.sh pclmul && echo -mpclmul || echo)
AVX=$(../Native/check_cpu.sh avx && echo -mavx || echo)
AVX2=$(../Native/check_cpu.sh avx2 && echo -mavx2 || echo)
AVX512F=$(../Native/check_cpu.sh avx512f && echo -mavx512f || echo)

export CPU_FLAGS="$AES $SSE2 $SSE3 $SSSE3 $PCLMUL $AVX $AVX2 $AVX512F"

HAVE_AES=$(../Native/check_cpu.sh aes && echo -D__AES__ || echo)
HAVE_SSE2=$(../Native/check_cpu.sh sse2 && echo -DHAVE_SSE2 || echo)
HAVE_SSE3=$(../Native/check_cpu.sh sse3 && echo -DHAVE_SSE3 || echo)
HAVE_SSSE3=$(../Native/check_cpu.sh ssse3 && echo -DHAVE_SSSE3 || echo)
HAVE_PCLMUL=$(../Native/check_cpu.sh pclmul && echo -DHAVE_PCLMUL || echo)
HAVE_AVX=$(../Native/check_cpu.sh avx && echo -DHAVE_AVX || echo)
HAVE_AVX2=$(../Native/check_cpu.sh avx2 && echo -DHAVE_AVX2 || echo)
HAVE_AVX512F=$(../Native/check_cpu.sh avx512f && echo -DHAVE_AVX512F || echo)

export HAVE_FEATURE="$HAVE_AES $HAVE_SSE2 $HAVE_SSE3 $HAVE_SSSE3 $HAVE_PCLMUL $HAVE_AVX $HAVE_AVX2 $HAVE_AVX512F"

(cd ../Native/libmultihash && make -j clean && make -j) && mv ../Native/libmultihash/libmultihash.so "$OutDir"
(cd ../Native/libetchash && make -j clean && make -j) && mv ../Native/libetchash/libetchash.so "$OutDir"
(cd ../Native/libethhash && make -j clean && make -j) && mv ../Native/libethhash/libethhash.so "$OutDir"
(cd ../Native/libethhashb3 && make -j clean && make -j) && mv ../Native/libethhashb3/libethhashb3.so "$OutDir"
(cd ../Native/libubqhash && make -j clean && make -j) && mv ../Native/libubqhash/libubqhash.so "$OutDir"
(cd ../Native/libcryptonote && make -j clean && make -j) && mv ../Native/libcryptonote/libcryptonote.so "$OutDir"
(cd ../Native/libcryptonight && make -j clean && make -j) && mv ../Native/libcryptonight/libcryptonight.so "$OutDir"
(cd ../Native/libkawpow && make -j clean && make -j) && mv ../Native/libkawpow/libkawpow.so "$OutDir"
(cd ../Native/libfiropow && make -j clean && make -j) && mv ../Native/libfiropow/libfiropow.so "$OutDir"
(cd ../Native/libevrprogpow && make -j clean && make -j) && mv ../Native/libevrprogpow/libevrprogpow.so "$OutDir"
(cd ../Native/libverushash && make clean && make) && mv ../Native/libverushash/libverushash.so "$OutDir"


((cd /tmp && rm -rf secp256k1 && git clone https://github.com/bitcoin-ABC/secp256k1 && cd secp256k1 && git checkout 04fabb44590c10a19e35f044d11eb5058aac65b2 && mkdir build && cd build && cmake -GNinja .. -DCMAKE_C_FLAGS=-fPIC -DSECP256K1_ENABLE_MODULE_RECOVERY=OFF -DSECP256K1_ENABLE_COVERAGE=OFF -DSECP256K1_ENABLE_MODULE_SCHNORR=ON && ninja) && (cd ../Native/libnexapow && cp /tmp/secp256k1/build/libsecp256k1.a . && make clean && make) && mv ../Native/libnexapow/libnexapow.so "$OutDir")
((cd /tmp && rm -rf RandomX && git clone https://github.com/tevador/RandomX && cd RandomX && git checkout tags/v1.1.10 && mkdir build && cd build && cmake -DARCH=native .. && make -j) && (cd ../Native/librandomx && cp /tmp/RandomX/build/librandomx.a . && make -j clean && make -j) && mv ../Native/librandomx/librandomx.so "$OutDir")
((cd /tmp && rm -rf RandomARQ && git clone https://github.com/arqma/RandomARQ && cd RandomARQ && git checkout 14850620439045b319fa6398f5a164715c4a66ce && mkdir build && cd build && cmake -DARCH=native .. && make -j) && (cd ../Native/librandomarq && cp /tmp/RandomARQ/build/librandomx.a . && make -j clean && make -j) && mv ../Native/librandomarq/librandomarq.so "$OutDir")
