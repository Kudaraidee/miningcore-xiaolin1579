#ifndef AURUM_H
#define AURUM_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stddef.h>
#include <stdint.h>

int PHS(void *output, size_t outlen, const void *input, size_t inlen, const void *salt, size_t saltlen, unsigned int t_cost, unsigned int m_cost);
void aurum_hash(const char *input, const char *output, uint32_t len);

#ifdef __cplusplus
}
#endif

#endif // AURUM_H