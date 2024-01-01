CFLAGS += -g -Wall -c -fPIC -O2 -Wno-pointer-sign -Wno-char-subscripts -Wno-unused-variable -Wno-unused-function -Wno-strict-aliasing -Wno-discarded-qualifiers -Wno-unused-const-variable $(CPU_FLAGS)
CXXFLAGS += -g -Wall -fPIC -fpermissive -O2 -faligned-new -Wno-char-subscripts -Wno-unused-variable -Wno-unused-function -Wno-strict-aliasing -Wno-sign-compare -std=c++11 $(CPU_FLAGS)
LDFLAGS = -shared
LDLIBS = -lsodium
TARGET = libverushash.so

OBJECTS = crypto/haraka.o crypto/haraka_portable.o crypto/ripemd160.o crypto/sha256.o crypto/uint256.o crypto/utilstrencodings.o crypto/verus_hash.o crypto/verus_clhash.o crypto/verus_clhash_portable.o exports.o verushashverify.o

all: $(TARGET)

$(TARGET): $(OBJECTS)
	$(CXX) $(LDFLAGS) -o $@ $^ $(LDLIBS)

.PHONY: clean

clean:
	$(RM) $(TARGET) $(OBJECTS)
