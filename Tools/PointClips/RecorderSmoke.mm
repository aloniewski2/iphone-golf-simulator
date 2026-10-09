// Compile with SportsPointRecorder.mm on macOS; exercises the real Metal→H.264 path.
#import <Foundation/Foundation.h>
#import <Metal/Metal.h>
#import "SportsPointRecorder.h"
#include <cstdio>
#include <cmath>
#include <atomic>
#include <mach/mach_time.h>
static std::atomic<bool> ready(false), saved(false), rejected(false), stopped(false), discarded(false);
extern "C" void SportsEmit(const char *json) {
    puts(json); fflush(stdout);
    NSDictionary *e=[NSJSONSerialization JSONObjectWithData:[[NSString stringWithUTF8String:json] dataUsingEncoding:NSUTF8StringEncoding] options:0 error:nil];
    if([e[@"state"] isEqual:@"ready"]) ready=true;
    if([e[@"state"] isEqual:@"waiting"] && ![e[@"hasClip"] boolValue]) discarded=true;
    if([e[@"state"] isEqual:@"saved"]) saved=true;
    if([e[@"state"] isEqual:@"unsupported"]) rejected=true;
}
int main(int argc,const char **argv) { @autoreleasepool {
    id activity=[NSProcessInfo.processInfo beginActivityWithOptions:NSActivityUserInitiated|NSActivityLatencyCritical reason:@"Real-time 60 fps recorder verification"];
    (void)activity;
    bool slow=argc>1 && strcmp(argv[1],"30")==0;
    bool pauseTest=argc>1 && strcmp(argv[1],"pause")==0;
    id<MTLDevice> device=MTLCreateSystemDefaultDevice(); id<MTLCommandQueue> queue=[device newCommandQueue];
    MTLTextureDescriptor *desc=[MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm width:1920 height:1080 mipmapped:NO]; desc.usage=MTLTextureUsageRenderTarget|MTLTextureUsageShaderRead; desc.storageMode=MTLStorageModePrivate;
    id<MTLTexture> texture=[device newTextureWithDescriptor:desc];
    SportsPointRecorder *rec=SportsPointRecorder.shared; [rec configureSession:@"smoke-test" display:0]; [rec setEnabled:YES];
    __block double point=0; __block bool ended=false, didPause=false; __block int frames=0;
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INTERACTIVE,0),^{
      mach_timebase_info_data_t timebase; mach_timebase_info(&timebase);
      uint64_t interval=(uint64_t)(1e9/(slow?30:60)*timebase.denom/timebase.numer), next=mach_absolute_time();
      while(!stopped) { @autoreleasepool {
        next+=interval; mach_wait_until(next);
        double now=NSProcessInfo.processInfo.systemUptime;
        if(ready && point==0) { [rec beginPoint]; point=now; }
        if(pauseTest && point>0 && now-point>.7 && !didPause) { [rec setPaused:YES]; [rec setPaused:NO]; [rec beginPoint]; point=now; didPause=true; }
        if(point>0 && now-point>2 && !ended) { [rec endPoint]; ended=true; }
        id<MTLCommandBuffer> cb=[queue commandBuffer]; MTLRenderPassDescriptor *pass=[MTLRenderPassDescriptor renderPassDescriptor]; pass.colorAttachments[0].texture=texture; pass.colorAttachments[0].loadAction=MTLLoadActionClear; pass.colorAttachments[0].storeAction=MTLStoreActionStore; pass.colorAttachments[0].clearColor=MTLClearColorMake((frames%60)/60.0,.25,.6,1);
        id<MTLRenderCommandEncoder> enc=[cb renderCommandEncoderWithDescriptor:pass]; [enc endEncoding]; [cb commit]; frames++;
        [rec captureTexture:texture queue:queue timestamp:NSProcessInfo.processInfo.systemUptime];
        float audio[1600]; for(int i=0;i<800;i++) audio[2*i]=audio[2*i+1]=.15f*sin((frames*800+i)*2*3.14159265359*440/48000);
        [rec captureAudio:audio count:1600 channels:2 rate:48000 timestamp:NSProcessInfo.processInfo.systemUptime];
        if(point>0 && now-point>3.5 && !saved) [rec saveLastPoint];
      } }
    });
    NSDate *deadline=[NSDate dateWithTimeIntervalSinceNow:14];
    while(!saved && !rejected && [deadline timeIntervalSinceNow]>0) [[NSRunLoop currentRunLoop] runUntilDate:[NSDate dateWithTimeIntervalSinceNow:.005]];
    stopped=true; [rec stop];
    bool passed=slow?rejected.load():saved.load() && (!pauseTest || discarded.load()); printf("Recorder smoke %s: %s\n",slow?"30fps rejection":pauseTest?"pause discards incomplete point":"60fps capture",passed?"PASS":"FAIL"); return passed?0:1;
} }
