#pragma once

#include <Windows.h>
#include <vcclr.h>
#include <atlstr.h>
#include <stdio.h>
#include <wchar.h>
#include <conio.h>
#include <tchar.h>
#include <map>

using namespace System;
using namespace System::Collections::Generic;

#include "TmsiSDK.h"

// Function pointers for SDK interactions
static POPEN fpOpen;
static PCLOSE fpClose;
static PSTART fpStart;
static PSTOP fpStop;
static PSETSIGNALBUFFER fpSetSignalBuffer;
static PGETSAMPLES fpGetSamples;
static PGETSIGNALFORMAT fpGetSignalFormat;
static PFREE fpFree;
static PLIBRARYINIT fpLibraryInit = nullptr;
static PLIBRARYEXIT fpLibraryExit;
static PGETFRONTENDINFO fpGetFrontEndInfo;
static PSETRTCTIME fpSetRtcTime;
static PGETRTCTIME fpGetRtcTime;
static PSETRTCALARMTIME fpSetRtcAlarmTime;
static PGETRTCALARMTIME fpGetRtcAlarmTime;
static PGETERRORCODE fpGetErrorCode;
static PGETERRORCODEMESSAGE fpGetErrorCodeMessage;
static PFREEDEVICELIST fpFreeDeviceList;
static PGETDEVICELIST fpGetDeviceList;
static PGETCONNECTIONPROPERTIES fpGetConnectionProperties;
static PSETMEASURINGMODE fpSetMeasuringMode;
static PSETREFCALCULATION fpSetRefCalculation;
static PGETBUFFERINFO fpGetBufferInfo;
static POPENFIRSTDEVICE fpOpenFirstDevice;

namespace RtDeviceDriver {

#define RTLOADER L"TMSiSDK.dll"

    /// <summary>
    /// RefaDriver handles communication with the TMSi device through the SDK.
    /// </summary>
    public ref class RefaDriver {
    private:
        unsigned int* SignalBuffer;  // Buffer to hold signal data
        unsigned long SignalBufferSizeInBytes;
        char** DeviceList = nullptr;
        HINSTANCE LibHandle;  // Handle to the dynamically loaded library
        HANDLE Handle;  // TMSi device handle
    public:
        int NrOfChannels;
        unsigned long SampleRateInHz;
        unsigned long SignalBufferSizeInSamples;
        bool Started;

    public:
        /// <summary>
        /// Constructor: Initializes the TMSi device driver.
        /// </summary>
        RefaDriver() { Initialize(); }

        /// <summary>
        /// Initializes the driver by loading the SDK and opening the first available device.
        /// </summary>
        void Initialize() {
            int ErrorCode = 0;
            try {
                // Load the TMSi SDK DLL
                LibHandle = LoadLibrary(TEXT("TMSiSDK.dll"));
                if (!LibHandle) throw gcnew Exception("Cannot open the driver DLL.");

                // Retrieve function pointers from the DLL
                fpOpen = (POPEN)GetProcAddress(LibHandle, "Open");
                fpClose = (PCLOSE)GetProcAddress(LibHandle, "Close");
                fpStart = (PSTART)GetProcAddress(LibHandle, "Start");
                fpStop = (PSTOP)GetProcAddress(LibHandle, "Stop");
                fpSetSignalBuffer = (PSETSIGNALBUFFER)GetProcAddress(LibHandle, "SetSignalBuffer");
                fpGetSamples = (PGETSAMPLES)GetProcAddress(LibHandle, "GetSamples");
                fpGetSignalFormat = (PGETSIGNALFORMAT)GetProcAddress(LibHandle, "GetSignalFormat");
                fpFree = (PFREE)GetProcAddress(LibHandle, "Free");
                fpLibraryInit = (PLIBRARYINIT)GetProcAddress(LibHandle, "LibraryInit");
                fpLibraryExit = (PLIBRARYEXIT)GetProcAddress(LibHandle, "LibraryExit");
                fpGetFrontEndInfo = (PGETFRONTENDINFO)GetProcAddress(LibHandle, "GetFrontEndInfo");
                fpSetMeasuringMode = (PSETMEASURINGMODE)GetProcAddress(LibHandle, "SetMeasuringMode");

                // Initialize library
                if (fpLibraryInit) Handle = fpLibraryInit(TMSiConnectionUSB, &ErrorCode);
                if (!Handle) throw gcnew Exception("TMSi Library initialization failed.");

                // Retrieve device list and open the first available device
                DeviceList = fpGetDeviceList(Handle, &ErrorCode);
                if (!DeviceList || ErrorCode) throw gcnew Exception("No TMSi device found.");
                if (!fpOpen(Handle, DeviceList[0])) throw gcnew Exception("Failed to open TMSi device.");
            }
            catch (Exception^ e) {
                throw e;
            }
        }

        /// <summary>
        /// Configures the device for impedance measurement mode.
        /// </summary>
        void InitiateImpedanceDataMode() {
            try {
                // Get signal format to determine the number of channels
                PSIGNAL_FORMAT psf = fpGetSignalFormat(Handle, nullptr);
                if (!psf) throw gcnew Exception("Cannot get signal format.");

                NrOfChannels = psf->Elements;
                fpFree(psf);

                // Configure buffer size
                SignalBufferSizeInSamples = 1000;
                ULONG temp = SignalBufferSizeInSamples;
                if (!fpSetSignalBuffer(Handle, nullptr, &temp))
                    throw gcnew Exception("Error in setting signal buffer.");
                SignalBufferSizeInSamples = temp;

                // Allocate memory for signal buffer
                SignalBufferSizeInBytes = SignalBufferSizeInSamples * NrOfChannels * sizeof(unsigned int);
                SignalBuffer = (unsigned int*)malloc(SignalBufferSizeInBytes);
                if (!SignalBuffer) throw gcnew Exception("Memory allocation failed for signal buffer.");

                // Start data acquisition
                if (!fpStart(Handle)) throw gcnew Exception("Failed to start data acquisition.");

                // Set measuring mode to impedance
                if (!fpSetMeasuringMode(Handle, MEASURE_MODE_IMPEDANCE_EX, IC_OHM_200))
                    throw gcnew Exception("Cannot enter impedance mode.");
            }
            catch (Exception^ e) {
                throw e;
            }
        }

        /// <summary>
        /// Retrieves impedance data from the device.
        /// </summary>
        /// <returns>Pointer to impedance data buffer.</returns>
        unsigned int* GetImpedances() {
            unsigned int BytesReturned = fpGetSamples(Handle, (PULONG)SignalBuffer, SignalBufferSizeInBytes);
            if (BytesReturned > 0) return SignalBuffer;
            throw gcnew Exception("Cannot retrieve impedance data.");
        }
    };
}
