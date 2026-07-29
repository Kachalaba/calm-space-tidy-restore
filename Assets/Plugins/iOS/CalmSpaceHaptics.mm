@import CoreHaptics;
@import Security;
@import UIKit;

namespace
{
    UIImpactFeedbackGenerator *s_dragGenerator = nil;
    UIImpactFeedbackGenerator *s_snapGenerator = nil;
    NSInteger s_hapticSupport = -1;

    bool HasHapticHardware()
    {
        if (s_hapticSupport >= 0)
        {
            return s_hapticSupport == 1;
        }

        bool supported = false;
        if (@available(iOS 13.0, *))
        {
            supported =
                [CHHapticEngine capabilitiesForHardware].supportsHaptics;
        }
        else if (@available(iOS 10.0, *))
        {
            supported = UIDevice.currentDevice.userInterfaceIdiom ==
                UIUserInterfaceIdiomPhone;
        }

        s_hapticSupport = supported ? 1 : 0;
        return supported;
    }

    void RunOnMainThread(dispatch_block_t block)
    {
        if ([NSThread isMainThread])
        {
            block();
            return;
        }

        dispatch_async(dispatch_get_main_queue(), block);
    }
}

extern "C"
{
    int CalmSpaceKeychainCopyOrCreateKey(
        const char *service,
        const char *account,
        unsigned char *output,
        int outputCapacity)
    {
        if (service == nullptr ||
            account == nullptr ||
            output == nullptr ||
            outputCapacity != 32)
        {
            return 0;
        }

        NSString *serviceName =
            [NSString stringWithUTF8String:service];
        NSString *accountName =
            [NSString stringWithUTF8String:account];
        if (serviceName == nil || accountName == nil)
        {
            return 0;
        }

        NSDictionary *query =
        @{
            (__bridge id)kSecClass:
                (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: serviceName,
            (__bridge id)kSecAttrAccount: accountName,
            (__bridge id)kSecReturnData: @YES,
            (__bridge id)kSecMatchLimit:
                (__bridge id)kSecMatchLimitOne
        };

        CFTypeRef result = nullptr;
        OSStatus status = SecItemCopyMatching(
            (__bridge CFDictionaryRef)query,
            &result);
        NSData *keyData = nil;
        if (status == errSecSuccess && result != nullptr)
        {
            keyData = CFBridgingRelease(result);
        }
        else if (status == errSecItemNotFound)
        {
            NSMutableData *generated =
                [NSMutableData dataWithLength:32];
            if (SecRandomCopyBytes(
                    kSecRandomDefault,
                    generated.length,
                    generated.mutableBytes) != errSecSuccess)
            {
                return 0;
            }

            NSDictionary *item =
            @{
                (__bridge id)kSecClass:
                    (__bridge id)kSecClassGenericPassword,
                (__bridge id)kSecAttrService: serviceName,
                (__bridge id)kSecAttrAccount: accountName,
                (__bridge id)kSecAttrAccessible:
                    (__bridge id)
                        kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
                (__bridge id)kSecValueData: generated
            };
            status = SecItemAdd(
                (__bridge CFDictionaryRef)item,
                nullptr);
            if (status == errSecDuplicateItem)
            {
                result = nullptr;
                status = SecItemCopyMatching(
                    (__bridge CFDictionaryRef)query,
                    &result);
                if (status == errSecSuccess && result != nullptr)
                {
                    keyData = CFBridgingRelease(result);
                }
            }
            else if (status == errSecSuccess)
            {
                keyData = generated;
            }
        }

        if (keyData.length != 32)
        {
            return 0;
        }

        [keyData getBytes:output length:32];
        return 32;
    }

    bool CalmSpaceHapticsIsSupported()
    {
        return HasHapticHardware();
    }

    void CalmSpaceHapticsPlayDragTick(float intensity)
    {
        if (!HasHapticHardware())
        {
            return;
        }

        const CGFloat normalized =
            MIN((CGFloat)1.0, MAX((CGFloat)0.0, (CGFloat)intensity));
        RunOnMainThread(^
        {
            if (@available(iOS 13.0, *))
            {
                if (s_dragGenerator == nil)
                {
                    s_dragGenerator = [[UIImpactFeedbackGenerator alloc]
                        initWithStyle:UIImpactFeedbackStyleSoft];
                }

                [s_dragGenerator
                    impactOccurredWithIntensity:0.22 + normalized * 0.28];
            }
            else if (@available(iOS 10.0, *))
            {
                if (s_dragGenerator == nil)
                {
                    s_dragGenerator = [[UIImpactFeedbackGenerator alloc]
                        initWithStyle:UIImpactFeedbackStyleLight];
                }

                [s_dragGenerator impactOccurred];
            }

            [s_dragGenerator prepare];
        });
    }

    void CalmSpaceHapticsPlaySnap()
    {
        if (!HasHapticHardware())
        {
            return;
        }

        RunOnMainThread(^
        {
            if (@available(iOS 13.0, *))
            {
                if (s_snapGenerator == nil)
                {
                    s_snapGenerator = [[UIImpactFeedbackGenerator alloc]
                        initWithStyle:UIImpactFeedbackStyleRigid];
                }

                [s_snapGenerator impactOccurredWithIntensity:0.82];
            }
            else if (@available(iOS 10.0, *))
            {
                if (s_snapGenerator == nil)
                {
                    s_snapGenerator = [[UIImpactFeedbackGenerator alloc]
                        initWithStyle:UIImpactFeedbackStyleMedium];
                }

                [s_snapGenerator impactOccurred];
            }

            [s_snapGenerator prepare];
        });
    }

    void CalmSpaceHapticsCancel()
    {
        RunOnMainThread(^
        {
            s_dragGenerator = nil;
            s_snapGenerator = nil;
        });
    }
}
