extern "C" {
    #include "RTDevice.h"
    #include "Feature.h" // If needed

    __declspec(dllexport) RTDeviceEx * CreateRTDeviceEx() {
        return new RTDeviceEx();
    }

    __declspec(dllexport) void DestroyRTDeviceEx(RTDeviceEx * device) {
        delete device;
    }

    __declspec(dllexport) BOOLEAN RTDevice_Start(RTDeviceEx * device) {
        return device->Start();
    }

    __declspec(dllexport) BOOLEAN RTDevice_Stop(RTDeviceEx * device) {
        return device->Stop();
    }

    // Add more exported functions here as required...
}
