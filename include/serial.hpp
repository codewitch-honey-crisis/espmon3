#pragma once
#include <stdint.h>
typedef struct {
    uint8_t cpu_tmax;
    uint8_t gpu_tmax;
    uint8_t cpu_usage;
    uint8_t cpu_temp;
    uint8_t gpu_usage;
    uint8_t gpu_temp;
} response_t;

bool serial_init();
void serial_write();
bool serial_read_packet(response_t* out_resp);
