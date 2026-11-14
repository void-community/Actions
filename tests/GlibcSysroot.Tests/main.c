#include <stdio.h>
int probe(void);
int main(void) { int result = probe(); printf("probe returned %d\n", result); return result; }
