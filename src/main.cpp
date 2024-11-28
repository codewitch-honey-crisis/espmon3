
#if __has_include(<Arduino.h>)
#include <Arduino.h>
#else
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "freertos/task.h"
#endif
#include <driver/gpio.h>
#include <driver/spi_master.h>
#include <memory.h>
#include <esp_lcd_panel_io.h>
#include <esp_lcd_panel_ops.h>
#include <esp_lcd_panel_vendor.h>
#define LCD_IMPLEMENTATION
#include "lcd_init.h"
#include "lcd_config.h"

#include <gfx.hpp>
#include <uix.hpp>
#include "serial.hpp"
#define BUNGEE_IMPLEMENTATION
#include "assets/bungee.h"

#ifdef M5STACK_CORE2
// these libs work with the ESP-IDF and Arduino
#include <esp_i2c.hpp>
#include <m5core2_power.hpp>
#endif
#ifdef ARDUINO
namespace arduino {}
using namespace arduino;
#else
namespace esp_idf {}
using namespace esp_idf;
static uint32_t millis() {
    return ((uint32_t)pdTICKS_TO_MS(xTaskGetTickCount()));
}
#endif


using namespace gfx;
using namespace uix;

// lcd data
#ifndef LCD_PIN_NUM_HSYNC
// This works out to be 32KB - the max DMA transfer size
static const size_t lcd_transfer_buffer_size = 32*1024;
#else
// This works out to be 64KB - no DMA
static const size_t lcd_transfer_buffer_size = 64*1024;
#endif
// for sending data to the display
static uint8_t *lcd_transfer_buffer = nullptr;
static uint8_t *lcd_transfer_buffer2 = nullptr;

static uix::display disp;
#ifdef M5STACK_CORE2
static m5core2_power power(esp_i2c<1,21,22>::instance);
#endif
#ifndef LCD_PIN_NUM_HSYNC
// indicates the LCD DMA transfer is complete
static bool lcd_flush_ready(esp_lcd_panel_io_handle_t panel_io,
                            esp_lcd_panel_io_event_data_t *edata,
                            void *user_ctx) {
    disp.flush_complete();
    return true;
}
#endif
// flush a bitmap to the display
static void uix_on_flush(const rect16& bounds,
                             const void *bitmap, void* state) {
    // adjust end coordinates for a quirk of Espressif's API (add 1 to each)
    lcd_panel_draw_bitmap(bounds.x1, bounds.y1, bounds.x2, bounds.y2,
                              (void *)bitmap);
#ifdef LCD_PIN_NUM_HSYNC
    disp.flush_complete();
#endif
}
void lcd_buffers_init() {
    // initialize the transfer buffers
    lcd_transfer_buffer = (uint8_t *)malloc(lcd_transfer_buffer_size);
    if (lcd_transfer_buffer == nullptr) {
        puts("Out of memory initializing primary transfer buffer");
        while (1) vTaskDelay(5);
    }
    memset(lcd_transfer_buffer, 0, lcd_transfer_buffer_size);
#ifndef LCD_PIN_NUM_HSYNC
    // initialize the transfer buffers
    lcd_transfer_buffer2 = (uint8_t *)malloc(lcd_transfer_buffer_size);
    if (lcd_transfer_buffer2 == nullptr) {
        puts("Out of memory initializing transfer buffer 2");
        while (1) vTaskDelay(5);
    }
    memset(lcd_transfer_buffer2, 0, lcd_transfer_buffer_size);
#else 
    lcd_transfer_buffer2 = nullptr;
#endif
}


const_buffer_stream text_font_stm(bungee,sizeof(bungee));

using screen_t = uix::screen<rgb_pixel<16>>;
using color_t = color<typename screen_t::pixel_type>;
using uix_color_t = color<rgba_pixel<32>>;
using vcolor_t = color<vector_pixel>;



template<typename ControlSurfaceType>
class vvert_label : public canvas_control<ControlSurfaceType> {
    using base_type = canvas_control<ControlSurfaceType>;
public:
    using type = vvert_label;
    using control_surface_type = ControlSurfaceType;
private:
    canvas_text_info m_label_text;
    canvas_path m_label_text_path;
    rectf m_label_text_bounds;
    bool m_label_text_dirty;
    vector_pixel m_color;
    void build_label_path_untransformed() {
        const float target_width = this->dimensions().height;
        float fsize = this->dimensions().width;
        m_label_text_path.initialize();
        do {
            m_label_text_path.clear();
            m_label_text.font_size = fsize;
            m_label_text_path.text({0.f,0.f},m_label_text);
            m_label_text_bounds = m_label_text_path.bounds(true);
            --fsize;
            
        } while(fsize>0.f && m_label_text_bounds.width()>=target_width);
    }
public:
    vvert_label() : base_type() ,m_label_text_dirty(true) {
        m_label_text.ttf_font = &text_font_stm;
        m_label_text.text_sz("Label");
        m_label_text.encoding = &text_encoding::utf8;
        m_label_text.ttf_font_face = 0;
        m_color = vector_pixel(255,255,255,255);
    }
    virtual ~vvert_label() {

    }
    text_handle text() const {
        return m_label_text.text;
    }
    void text(text_handle text, size_t text_byte_count) {
        m_label_text.text=text;
        m_label_text.text_byte_count = text_byte_count;
        m_label_text_dirty = true;
        this->invalidate();
    }
    void text(const char* sz) {
        m_label_text.text_sz(sz);
        m_label_text_dirty = true;
        this->invalidate();
    }
    rgba_pixel<32> color() const {
        rgba_pixel<32> result;
        convert(m_color,&result);
        return result;
    }
    void color(rgba_pixel<32> value) {
        convert(value,&m_color);
        this->invalidate();
    }
protected:
    virtual void on_before_paint() override {
        if(m_label_text_dirty) {
            build_label_path_untransformed();
            m_label_text_dirty = false;
        }
    }
    virtual void on_paint(canvas& destination, const srect16& clip) override {
        canvas_style si = destination.style();
        si.fill_paint_type = paint_type::solid;
        si.stroke_paint_type = paint_type::none;
        si.fill_color = m_color;
        destination.style(si);
        // save the current transform
        matrix old = destination.transform();
        matrix m = old.rotate(math::deg2rad(-90));
        
        m=m.translate(-m_label_text_bounds.width()-((destination.dimensions().height-m_label_text_bounds.width())*0.5f),m_label_text_bounds.height());
        destination.transform(m);
        destination.path(m_label_text_path);
        destination.render();
        // restore the old transform
        destination.transform(old);
    }
};
using vert_label_t = vvert_label<screen_t::control_surface_type>;

template<typename ControlSurfaceType>
class vlabel : public canvas_control<ControlSurfaceType> {
    using base_type = canvas_control<ControlSurfaceType>;
public:
    using type = vlabel;
    using control_surface_type = ControlSurfaceType;
private:
    canvas_text_info m_label_text;
    canvas_path m_label_text_path;
    rectf m_label_text_bounds;
    bool m_label_text_dirty;
    vector_pixel m_color;
    rgba_pixel<32> m_back_color;
    void build_label_path_untransformed() {
        const float target_width = this->dimensions().width;
        float fsize = this->dimensions().height;
        m_label_text_path.initialize();
        do {
            m_label_text_path.clear();
            m_label_text.font_size = fsize;
            m_label_text_path.text({0.f,0.f},m_label_text);
            m_label_text_bounds = m_label_text_path.bounds(true);
            --fsize;
            
        } while(fsize>0.f && m_label_text_bounds.width()>=target_width);
    }
public:
    vlabel() : base_type() ,m_label_text_dirty(true) {
        m_label_text.ttf_font = &text_font_stm;
        m_label_text.text_sz("Label");
        m_label_text.encoding = &text_encoding::utf8;
        m_label_text.ttf_font_face = 0;
        m_color = vector_pixel(255,255,255,255);
        m_back_color = rgba_pixel<32>(0,true);
    }
    virtual ~vlabel() {

    }
    text_handle text() const {
        return m_label_text.text;
    }
    void text(text_handle text, size_t text_byte_count) {
        m_label_text.text=text;
        m_label_text.text_byte_count = text_byte_count;
        m_label_text_dirty = true;
        this->invalidate();
    }
    void text(const char* sz) {
        m_label_text.text_sz(sz);
        m_label_text_dirty = true;
        this->invalidate();
    }
    rgba_pixel<32> color() const {
        rgba_pixel<32> result;
        convert(m_color,&result);
        return result;
    }
    void color(rgba_pixel<32> value) {
        convert(value,&m_color);
        this->invalidate();
    }
    rgba_pixel<32> back_color() const {
        return m_back_color;
    }
    void back_color(rgba_pixel<32> value) {
        m_back_color = value;
        this->invalidate();
    }
protected:
    virtual void on_before_paint() override {
        if(m_label_text_dirty) {
            build_label_path_untransformed();
            m_label_text_dirty = false;
        }
    }
    virtual void on_paint(control_surface_type& destination, const srect16& clip) {
        if(m_back_color.opacity()!=0) {
            draw::filled_rectangle(destination,destination.bounds(),m_back_color);
        }
        base_type::on_paint(destination,clip);
    }
    virtual void on_paint(canvas& destination, const srect16& clip) override {
        canvas_style si = destination.style();
        si.fill_paint_type = paint_type::solid;
        si.stroke_paint_type = paint_type::none;
        si.fill_color = vcolor_t::white;
        destination.style(si);
        // save the current transform
        matrix old = destination.transform();
        matrix m = matrix::create_identity();
        m=m.translate(-m_label_text_bounds.x1,(-m_label_text_bounds.y1)+(destination.dimensions().height-m_label_text_bounds.height())*0.5f);
        destination.transform(m);
        destination.path(m_label_text_path);
        destination.render();
        // restore the old transform
        destination.transform(old);
    }

};
using label_t = vlabel<screen_t::control_surface_type>;

template<typename ControlSurfaceType>
class bar : public control<ControlSurfaceType> {
    using base_type = control<ControlSurfaceType>;
public:
    using type = bar;
    using control_surface_type = ControlSurfaceType;
private:
    rgba_pixel<32> m_color;
    rgba_pixel<32> m_back_color;
    bool m_is_gradient;
    float m_value;
public:
    bar() : base_type(), m_is_gradient(false), m_value(0) {
        static constexpr const rgb_pixel<24> px(0,255,0);
        static constexpr const rgb_pixel<24> black(0,0,0);
        convert(px,&m_color);
        rgba_pixel<32> px2;
        convert(black,&px2);
        m_back_color = m_color.blend(px2,.125f);
    }
    
    virtual ~bar() {

    }
    float value() const {
        return m_value;
    }
    void value(float value) {
        value = math::clamp(0.f,value,1.f);
        if(value!=m_value) {
            m_value = value;
            this->invalidate();
        }
    }
    bool is_gradient() const {
        return m_is_gradient;
    }
    void is_gradient(bool value) {
        m_is_gradient= value;
        this->invalidate();
    }
    rgba_pixel<32> color() const {
        return m_color;
    }
    void color(rgba_pixel<32> value) {
        m_color=value;
        this->invalidate();
    }
    rgba_pixel<32> back_color() const {
        return m_back_color;
    }
    void back_color(rgba_pixel<32> value) {
        m_back_color  = value;
        this->invalidate();
    }
protected:
    virtual void on_paint(control_surface_type& destination, const srect16& clip) {
        typename control_surface_type::pixel_type scr_bg;
        destination.point({0,0},&scr_bg);
        uint16_t x_end = roundf(m_value*destination.dimensions().width-1);
        uint16_t y_end = destination.dimensions().height-1;
        if(m_is_gradient) {
            y_end=destination.dimensions().height*.6666;
            // two reference points for the ends of the graph
            hsva_pixel<32> px = gfx::color<gfx::hsva_pixel<32>>::red;
            hsva_pixel<32> px2 = gfx::color<gfx::hsva_pixel<32>>::green;
            auto h1 = px.channel<channel_name::H>();
            auto h2 = px2.channel<channel_name::H>();
            // adjust so we don't overshoot
            h2 -= 64;
            // the actual range we're drawing
            auto range = abs(h2 - h1) + 1;
            // the width of each gradient segment
            int w = (int)ceilf(destination.dimensions().width / 
                                (float)range) + 1;                
            // the step of each segment - default 1
            int s = 1;
            // if the gradient is larger than the control
            if (destination.dimensions().width < range) {
                // change the segment to width 1
                w = 1;
                // and make its step larger
                s = range / (float)destination.dimensions().width;  
            } 
            int x = 0;
            // c is the current color offset
            // it increases by s (step)
            int c = 0;
            // for each color in the range
            for (auto j = 0; j < range; ++j) {
                // adjust the H value (inverted and offset)
                px.channel<channel_name::H>(range - c - 1 + h1);
                // if we're drawing the filled part
                // it's fully opaque
                // otherwise it's semi-transparent
                int sw = w;
                int diff=0;
                if (m_value==0||x> x_end) {
                    px.channel<channel_name::A>(95);
                    if((x-w)<=x_end) {
                        sw = x_end-x+1;
                        diff = w-sw;
                    }
                } else {
                    px.channel<channel_name::A>(255);
                }
                // create the rect for our segment
                srect16 r(x, y_end+1, x + sw , destination.dimensions().height-1);
                
                // black out the area underneath so alpha blending
                // works correctly
                draw::filled_rectangle(destination, 
                                    r, 
                                    scr_bg
                                    );
                // draw the segment
                draw::filled_rectangle(destination, 
                                    r, 
                                    px 
                                    );
                if(diff>0) {
                    r=srect16(x+sw,y_end+1,x+w,destination.dimensions().height-1);
                    draw::filled_rectangle(destination, 
                                    r, 
                                    scr_bg
                                    );
                    // draw the segment
                    draw::filled_rectangle(destination, 
                                    r, 
                                    px 
                                    );
                }
                // increment
                x += w;
                c += s;
            }
        } 
        if(m_value>0) {
            draw::filled_rectangle(destination,srect16(0,0,x_end,y_end),m_color);
            draw::filled_rectangle(destination,srect16(x_end+1,0,destination.dimensions().width-1,y_end),m_back_color);
        } else {
            draw::filled_rectangle(destination,srect16(0,0,destination.dimensions().width-1,y_end),m_back_color);
        }
    }
};
using bar_t = bar<screen_t::control_surface_type>;
template<typename ControlSurfaceType>
class vgraph : public control<ControlSurfaceType> {
    using base_type = control<ControlSurfaceType>;
    using buffer_t = data::circular_buffer<uint8_t,100>;
public:
    using type = vgraph;
    using control_surface_type = ControlSurfaceType;
private:
    struct data_line {
        rgba_pixel<32> color;
        buffer_t buffer;
        data_line* next;
    };
    data_line* m_first;
    void clear_lines() {
        data_line*entry=m_first;
        while(entry!=nullptr) {
            data_line* n = entry->next;
            delete entry;
            entry = n;
        }
        m_first = nullptr;
    }
public:
    vgraph() : base_type(), m_first(nullptr) {
    }
    virtual ~vgraph() {
        clear_lines();
    }
    void remove_lines() {
        clear_lines();
        this->invalidate();
    }
    size_t add_line(rgba_pixel<32> color) {
        data_line* n;
        if(m_first==nullptr) {
            n = new data_line();
            if(n==nullptr) {
                return 0; // out of memory
            }
            n->color = color;
            n->next = nullptr;
            m_first = n;
            return 1;
        }
        size_t result = 0;
        data_line*entry=m_first;
        while(entry!=nullptr) {
            n = entry->next;
            if(n==nullptr) {
                n = new data_line();
                if(n==nullptr) {
                    return 0; // out of memory
                }
                n->color =color;
                n->next = nullptr;
                entry->next = n;
                break;
            }
            entry = n;
            ++result;
        }
        this->invalidate();
        return result+1;
    }
    bool add_data(size_t line_index,float value) {
        uint8_t v = math::clamp(0.f,value,1.f)*255;
        size_t i = 0;
        for(data_line* entry = m_first;entry!=nullptr;entry=entry->next) {
            if(i==line_index) {
                if(entry->buffer.size()==entry->buffer.capacity) {
                    uint8_t tmp;
                    entry->buffer.get(&tmp);
                }
                entry->buffer.put(v);
                this->invalidate();
                return true;
            }
            ++i;
        }
        return false;
    }
    void clear_data() {
        for(data_line* entry = m_first;entry!=nullptr;entry=entry->next) {
            entry->buffer.clear();
        }
        this->invalidate();
    }
protected:
    void on_paint(control_surface_type& destination, const srect16& clip) {
        srect16 b = (srect16)destination.bounds();
        auto px = gfx::color<typename control_surface_type::pixel_type>::gray;
        draw::rectangle(destination,b,px);
        b.inflate_inplace(-1,-1);
        const float tenth_x = ((float)b.width())/10.f;
        for(float x = b.x1;x<=b.x2;x+=tenth_x) {
            destination.fill(rect16(x,b.y1,x,b.y2),px);
        }
        const float tenth_y = ((float)b.height())/10.f;
        for(float y = b.y1;y<=b.y2;y+=tenth_y) {
            destination.fill(rect16(b.x1,y,b.x2,y),px);
        }
        for(data_line* entry = m_first;entry!=nullptr;entry=entry->next) {
            if(entry->buffer.size()) {
                uint8_t v = *entry->buffer.peek(0);
                float fv = v/255.f;
                float y = (1.0-fv)*(tenth_y*10);
                pointf pt(b.x1,y);
                for(int i = 1;i<entry->buffer.size();++i) {
                    v = *entry->buffer.peek(i);
                    fv = v/255.f;
                    pointf pt2=pt;
                    pt2.x+=(tenth_x*.1f);
                    y = (1.f-fv)*(tenth_y*10);
                    pt2.y =y;
                    draw::line(destination,srect16(roundf(pt.x),roundf(pt.y),roundf(pt2.x),roundf(pt2.y)),entry->color);
                    pt=pt2;
                }
            }
        }
    }
};
using graph_t = vgraph<screen_t::control_surface_type>;

static screen_t main_screen;
static vert_label_t cpu_label;
static vert_label_t gpu_label;

static label_t cpu_usage_label;
static label_t cpu_heat_label;

static bar_t cpu_usage_bar;
static bar_t cpu_heat_bar;

static label_t gpu_usage_label;
static label_t gpu_heat_label;

static bar_t gpu_usage_bar;
static bar_t gpu_heat_bar;

static vert_label_t gpu_usage_meter;

static graph_t history_graph;

static char cpu_usage_text[32];
static char cpu_heat_text[32];
static char gpu_usage_text[32];
static char gpu_heat_text[32];
    
static label_t disconnected_label;

#ifdef ARDUINO
void setup() {
    setCpuFrequencyMhz(240);
#else
void loop();
static void loop_task(void* arg) {
    while(1) {
        static int count = 0;
        loop();
        // tickle the watchdog periodically
        if (count++ == 4) {
            count = 0;
            vTaskDelay(5);
        }
    }
}

extern "C" void app_main() {
#endif
#ifdef M5STACK_CORE2
    power.initialize();
    power.lcd_voltage(3);
    power.power_led_enable(false);
#endif
    lcd_buffers_init();
#ifdef LCD_PIN_NUM_HSYNC
    lcd_panel_init();
#else
    lcd_panel_init(lcd_transfer_buffer_size,lcd_flush_ready);
#endif
    serial_init();
    disp.buffer_size(lcd_transfer_buffer_size);
    disp.buffer1(lcd_transfer_buffer);
    disp.buffer2(lcd_transfer_buffer2);
    disp.on_flush_callback(uix_on_flush);
    disp.on_touch_callback(nullptr);
    main_screen.dimensions({LCD_WIDTH,LCD_HEIGHT});
    main_screen.background_color(gfx::color<typename screen_t::pixel_type>::black);
    cpu_label.bounds(srect16(0,0,(main_screen.dimensions().width)/10-1,main_screen.dimensions().height/4).inflate(-2,-4));
    cpu_label.text("CPU");
    cpu_label.color(uix_color_t::light_blue);
    main_screen.register_control(cpu_label);
    srect16 b = cpu_label.bounds();
    cpu_usage_label.bounds(srect16(b.x2+2,b.y1,b.x2+1+(main_screen.dimensions().width/5),b.height()/2+b.y1));
    cpu_usage_label.text("---");
    strcpy(cpu_usage_text,"---");
    main_screen.register_control(cpu_usage_label);
    b = cpu_usage_label.bounds();
    cpu_heat_label.bounds(srect16(b.x1,b.y2+1,b.x2,b.y2+b.height()));
    cpu_heat_label.text("---");
    strcpy(cpu_heat_text,"---");
    main_screen.register_control(cpu_heat_label);

    b=cpu_usage_label.bounds();
    b.x1 = b.x2+4;
    b.x2 = main_screen.dimensions().width-1;
    b.y2-=2;
    cpu_usage_bar.bounds(b);
    cpu_usage_bar.value(0);
    main_screen.register_control(cpu_usage_bar);

    b=cpu_heat_label.bounds();
    b.x1 = b.x2+4;
    b.x2 = main_screen.dimensions().width-1;
    b.y2-=2;
    cpu_heat_bar.bounds(b);
    auto px = uix_color_t::orange;
    cpu_heat_bar.color(px);
    cpu_heat_bar.is_gradient(true);
    cpu_heat_bar.back_color(px.opacity(.125));
    cpu_heat_bar.value(0);
    main_screen.register_control(cpu_heat_bar);
    

    gpu_label.bounds(cpu_label.bounds().offset(0,main_screen.dimensions().height/4+3));
    gpu_label.color(uix_color_t::light_salmon);
    gpu_label.text("GPU");
    main_screen.register_control(gpu_label);
    b = gpu_label.bounds();
    gpu_usage_label.bounds(srect16(b.x2+2,b.y1,b.x2+1+(main_screen.dimensions().width/5),b.height()/2+b.y1));
    gpu_usage_label.text("---");
    strcpy(gpu_usage_text,"---");
    main_screen.register_control(gpu_usage_label);
    b = gpu_usage_label.bounds();
    gpu_heat_label.bounds(srect16(b.x1,b.y2+1,b.x2,b.y2+b.height()));
    gpu_heat_label.text("---"); // \xC2\xB0
    strcpy(gpu_heat_text,"---");
    main_screen.register_control(gpu_heat_label);
    
    b=gpu_usage_label.bounds();
    b.x1 = b.x2+4;
    b.x2 = main_screen.dimensions().width-1;
    b.y2-=2;
    gpu_usage_bar.bounds(b);
    gpu_usage_bar.value(0);
    gpu_usage_bar.color(uix_color_t::pale_goldenrod);
    gpu_usage_bar.back_color(uix_color_t::pale_goldenrod.opacity(.125));
    main_screen.register_control(gpu_usage_bar);

    b=gpu_heat_label.bounds();
    b.x1 = b.x2+4;
    b.x2 = main_screen.dimensions().width-1;
    b.y2-=2;
    gpu_heat_bar.bounds(b);
    px = uix_color_t::purple;
    gpu_heat_bar.color(px);
    gpu_heat_bar.is_gradient(true);
    gpu_heat_bar.back_color(px.opacity(.125));
    gpu_heat_bar.value(0);
    main_screen.register_control(gpu_heat_bar);
    

    b = main_screen.bounds();
    b.y1=main_screen.dimensions().height/2+1;
    history_graph.bounds(b);
    history_graph.add_line(cpu_usage_bar.color());
    history_graph.add_line(cpu_heat_bar.color());
    history_graph.add_line(gpu_usage_bar.color());
    history_graph.add_line(gpu_heat_bar.color());
    main_screen.register_control(history_graph);

    disconnected_label.bounds(srect16(0,0,main_screen.dimensions().width/2,main_screen.dimensions().width/8).center(main_screen.bounds()));
    rgba_pixel<32> bg = uix_color_t::black;
    bg.opacity_inplace(.6f);
    disconnected_label.back_color(bg);
    disconnected_label.text("[ disconnected ]");
    main_screen.register_control(disconnected_label);

    disp.active_screen(main_screen);
    disp.update();
#ifndef ARDUINO
    TaskHandle_t loop_handle;
    xTaskCreate(loop_task,"loop_task",4096,nullptr,20,&loop_handle);
#endif
}

void loop() {
    struct to_avg {
        float g0,g1,g2,g3;
    };
    static to_avg values[5];
    static uint32_t ts = 0;
    static int ts_count = 0;
    static int index =0;
    if(millis()>=ts+100) {
        ts=millis();
        ++ts_count;
        serial_write();
    }
    float v;
    size_t len;
    
    to_avg& value=values[index];
    response_t resp; 
    while(serial_read_packet(&resp)) {
        ts_count = 0;
        if(disconnected_label.visible()) {
            disconnected_label.visible(false);
            disp.update();
        }
        v=((float)resp.cpu_usage)/255.f;
        value.g0=v;
        itoa((int)roundf(v*100.f),cpu_usage_text,10);
        len = strlen(cpu_usage_text);
        cpu_usage_text[len]='%';
        cpu_usage_text[len+1]=0;
        cpu_usage_label.text(cpu_usage_text);
        disp.update();
        cpu_usage_bar.value(v);
        disp.update();
        v=((float)resp.cpu_temp)/255.f;
        value.g1=v;
        itoa((int)roundf(v*resp.cpu_tmax),cpu_heat_text,10);
        len = strlen(cpu_heat_text);
        cpu_heat_text[len]='\xC2';
        cpu_heat_text[len+1]='\xB0';
        cpu_heat_text[len+2]=0;
        cpu_heat_label.text(cpu_heat_text);
        disp.update();
        cpu_heat_bar.value(v);
        disp.update();
        v=((float)resp.gpu_usage)/255.f;
        value.g2=v;
        itoa((int)roundf(v*100.f),gpu_usage_text,10);
        len = strlen(gpu_usage_text);
        gpu_usage_text[len]='%';
        gpu_usage_text[len+1]=0;
        gpu_usage_label.text(gpu_usage_text);
        disp.update();
        gpu_usage_bar.value(v);
        disp.update();
        v=((float)resp.gpu_temp)/255.f;
        value.g3=v;
        itoa((int)roundf(v*resp.gpu_tmax),gpu_heat_text,10);
        len = strlen(gpu_heat_text);
        gpu_heat_text[len]='\xC2';
        gpu_heat_text[len+1]='\xB0';
        gpu_heat_text[len+2]=0;
        gpu_heat_label.text(gpu_heat_text);
        disp.update();
        gpu_heat_bar.value(v);
        disp.update();
        
        ++index;
    }
    if(index>=5) {
        index = 0;
        to_avg total;
        memset(&total,0,sizeof(total));
        for(int i = 0;i<5;++i) {
            total.g0+=values[i].g0;
            total.g1+=values[i].g1;
            total.g2+=values[i].g2;
            total.g3+=values[i].g3;
    
        }
        total.g0/=5.f;
        total.g1/=5.f;
        total.g2/=5.f;
        total.g3/=5.f;
        history_graph.add_data(0,total.g0);
        history_graph.add_data(1,total.g1);
        history_graph.add_data(2,total.g2);
        history_graph.add_data(3,total.g3);
        disp.update();   
    }
    
    if(ts_count>=10) { // 1 second
        ts_count = 0;
        index = 0;
        cpu_usage_label.text("---");
        disp.update();
        cpu_usage_bar.value(0);
        disp.update();
        cpu_heat_label.text("---");
        disp.update();
        cpu_heat_bar.value(0);
        disp.update();
        gpu_usage_label.text("---");
        disp.update();
        gpu_usage_bar.value(0);
        disp.update();
        gpu_heat_label.text("---");
        disp.update();
        gpu_heat_bar.value(0);
        disp.update();
        history_graph.clear_data();
        disp.update();
        disconnected_label.visible(true);
        disp.update();
    }

}
