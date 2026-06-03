#import <Foundation/Foundation.h>
#import <AppMetricaCore/AppMetricaCore.h>

extern "C" void AppMetricaLiteActivate(const char *apiKey)
{
    if (apiKey == NULL || apiKey[0] == '\0') {
        NSLog(@"[AppMetricaLite] Activation skipped: API key is empty.");
        return;
    }

    NSString *key = [NSString stringWithUTF8String:apiKey];
    if (key.length == 0) {
        NSLog(@"[AppMetricaLite] Activation skipped: API key is invalid.");
        return;
    }

    if (AMAAppMetrica.isActivated) {
        NSLog(@"[AppMetricaLite] AppMetrica is already activated.");
        return;
    }

    AMAAppMetricaConfiguration *configuration = [[AMAAppMetricaConfiguration alloc] initWithAPIKey:key];
    [AMAAppMetrica activateWithConfiguration:configuration];
    NSLog(@"[AppMetricaLite] AppMetricaCore activated.");
}
