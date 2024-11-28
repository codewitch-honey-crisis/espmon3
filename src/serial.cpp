#include <memory.h>
#include <esp_idf_version.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <freertos/queue.h>
#include <driver/uart.h>
#include <driver/gpio.h>
#include <esp_err.h>
#include <esp_log.h>
#include "serial.hpp"
#define SERIAL_QUEUE_SIZE 64
#define SERIAL_BUF_SIZE (2*SERIAL_QUEUE_SIZE)
const char* TAG = "Serial";

bool serial_read_packet(response_t* out_resp) {
    uint8_t tmp;
    if(1==uart_read_bytes(UART_NUM_0,&tmp,1,0)) {
        if(tmp==1) {
            return 0!=uart_read_bytes(UART_NUM_0,out_resp,sizeof(response_t),portMAX_DELAY);
        } else {
            while(uart_read_bytes(UART_NUM_0,&tmp,1,0)>0) vTaskDelay(5);
        }
    }
    return false;
}
void serial_write() {
    char c = 1;
    uart_write_bytes(UART_NUM_0,&c,1);
}
bool serial_init() {
    esp_log_level_set(TAG, ESP_LOG_INFO);
    
    /* Configure parameters of an UART driver,
     * communication pins and install the driver */
    uart_config_t uart_config;
    memset(&uart_config,0,sizeof(uart_config));
    uart_config.baud_rate = 115200;
    uart_config.data_bits = UART_DATA_8_BITS;
    uart_config.parity = UART_PARITY_DISABLE;
    uart_config.stop_bits = UART_STOP_BITS_1;
    uart_config.flow_ctrl = UART_HW_FLOWCTRL_DISABLE;
    //Install UART driver, and get the queue.
    if(ESP_OK!=uart_driver_install(UART_NUM_0, SERIAL_BUF_SIZE * 2, 0, 20, nullptr, 0)) {
        ESP_LOGE(TAG,"Unable to install uart driver");
        goto error;
    }
    uart_param_config(UART_NUM_0, &uart_config);
    //Set UART pins (using UART0 default pins ie no changes.)
    uart_set_pin(UART_NUM_0, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE);
    //Create a task to handler UART event from ISR
    
    return true;
error:
  
    return false;
}