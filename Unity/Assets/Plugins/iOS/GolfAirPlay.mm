// The system AirPlay picker, so the player can send the game's sound to a Mac or Apple TV from
// inside the game. The picture is the phone's Screen Mirroring (Control Center), which an app
// cannot start itself; once it is on, iOS hands the game a second display and BigScreen takes it.
// Called from GolfArcade.Game.BigScreen via DllImport("__Internal").
#import <UIKit/UIKit.h>
#import <AVKit/AVKit.h>

static AVRoutePickerView *picker;

extern "C" {

void GolfAirPlay_ShowPicker(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIWindow *window = nil;
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
            if ([scene isKindOfClass:UIWindowScene.class])
                for (UIWindow *w in ((UIWindowScene *)scene).windows) if (w.isKeyWindow) window = w;
        if (!window) window = UIApplication.sharedApplication.windows.firstObject;
        if (!window) return;
        if (!picker)
        {
            picker = [[AVRoutePickerView alloc] initWithFrame:CGRectMake(0, 0, 1, 1)];
            picker.prioritizesVideoDevices = YES;
            picker.alpha = 0.02;
            [window addSubview:picker];
        }
        for (UIView *sub in picker.subviews)
            if ([sub isKindOfClass:UIButton.class]) { [(UIButton *)sub sendActionsForControlEvents:UIControlEventTouchUpInside]; return; }
    });
}

int GolfAirPlay_ScreenCount(void)
{
    return (int)UIScreen.screens.count;
}

}
