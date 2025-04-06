// RtDeviceDriver.h

#pragma once

#include <Windows.h>
#include <vcclr.h> 
#include<atlstr.h>

using namespace System;

#include <stdio.h>
#include <wchar.h>
#include <conio.h>
#include <tchar.h> 
#include <map>

using namespace System;
using namespace Collections;
using namespace Collections::Generic;

#include "TmsiSDK.h" 

static POPEN fpOpen;
static PCLOSE fpClose;
static PSTART fpStart;
static PSTOP fpStop;
static PSETSIGNALBUFFER fpSetSignalBuffer;
static PGETSAMPLES	fpGetSamples;
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

// Functions for Mobita
static PSTARTCARDFILE fpStartCardFile;
static PSTOPCARDFILE fpStopCardFile;
static PGETCARDFILESAMPLES fpGetCardFileSamples;
static PGETCARDFILESIGNALFORMAT fpGetCardFileSignalFormat;
static POPENCARDFILE fpOpenCardFile;
static PGETCARDFILELIST fpGetCardFileList;
static PCLOSECARDFILE fpCloseCardFile;
static PGETRECORDINGCONFIGURATION fpGetRecordingConfiguration;
static PSETRECORDINGCONFIGURATION fpSetRecordingConfiguration;
static PGETEXTFRONTENDINFO fpGetExtFrontEndInfo;

//Functions for Nexus10-MKII
static PGETRANDOMKEY fpGetRandomKey;
static PUNLOCKFRONTEND fpUnlockFrontEnd;
static PGETOEMSIZE fpGetOEMSize;
static PSETOEMDATA fpSetOEMData;
static PGETOEMDATA fpGetOEMData;
static PSETSTORAGEMODE fpSetStorageMode;

static PGETDIGSENSORID fpGetDigSensorId;
static PGETDIGSENSORCONFIG fpGetDigSensorConfig;
static PGETDIGSENSORDATA fpGetDigSensorData;
static PSETDIGSENSORDATA fpSetDigSensorData;

// Functions for Nexus
static PGETFLASHSTATUS fpGetFlashStatus;
static PSTARTFLASHDATA fpStartFlashData;
static PGETFLASHSAMPLES fpGetFlashSamples;
static PSTOPFLASHDATA fpStopFlashData;
static PFLASHERASEMEMORY fpFlashEraseMemory;
static PSETFLASHDATA fpSetFlashData;

static POPENFIRSTDEVICE fpOpenFirstDevice;

namespace RtDeviceDriver {

	//#define RTLOADER L"RTINST.Dll"
#define RTLOADER L"TMSiSDK.dll"


	public ref class RefaDriver
	{
		//Device Handle Master
		unsigned int* SignalBuffer, SignalBufferSizeInBytes;
		char** DeviceList = NULL;
		//	static int NrOfDevices = 0;

		//  lib
		//--------


	public: property HINSTANCE LibHandle;
	public: property int NrOfChannels;
	public: property unsigned long SampleRateInHz;
	public: property unsigned long SignalBufferSizeInSamples;
	public: property bool Started;
	public: property HANDLE Handle;


		  RefaDriver() {
			  Initialize();
		  }
		  int fileExists(TCHAR* file)
		  {
			  WIN32_FIND_DATA FindFileData;
			  HANDLE handle = FindFirstFile(file, &FindFileData);
			  int found = handle != INVALID_HANDLE_VALUE;
			  if (found)
			  {
				  //FindClose(&handle); this will crash
				  FindClose(handle);
			  }
			  return found;
		  }

	public: void Initialize()
	{
		int ErrorCode = 0;

		try {
			//TCHAR Path[MAX_PATH];
			//GetSystemDirectory(Path, sizeof(Path) / sizeof(TCHAR));
			//lstrcat(Path, L"C:\\Windows\\System32\\TMSiSDK.dll");
			//if (fileExists(Path))
			try {
				LibHandle = LoadLibrary(TEXT("C:\\Windows\\System32\\TMSiSDK.dll"));
			}
			catch (Exception^ e)
			{
				throw gcnew Exception("Cannot open the driver dll.");
			}
			int NrOfDevices = 0;

			fpOpen = (POPEN)GetProcAddress(LibHandle, "Open");
			fpClose = (PCLOSE)GetProcAddress(LibHandle, "Close");
			fpStart = (PSTART)GetProcAddress(LibHandle, "Start");
			fpStop = (PSTOP)GetProcAddress(LibHandle, "Stop");
			fpSetSignalBuffer = (PSETSIGNALBUFFER)GetProcAddress(LibHandle, "SetSignalBuffer");
			fpGetSamples = (PGETSAMPLES)GetProcAddress(LibHandle, "GetSamples");
			fpGetBufferInfo = (PGETBUFFERINFO)GetProcAddress(LibHandle, "GetBufferInfo");
			fpGetSignalFormat = (PGETSIGNALFORMAT)GetProcAddress(LibHandle, "GetSignalFormat");
			fpFree = (PFREE)GetProcAddress(LibHandle, "Free");
			fpLibraryInit = (PLIBRARYINIT)GetProcAddress(LibHandle, "LibraryInit");
			fpLibraryExit = (PLIBRARYEXIT)GetProcAddress(LibHandle, "LibraryExit");
			fpGetFrontEndInfo = (PGETFRONTENDINFO)GetProcAddress(LibHandle, "GetFrontEndInfo");
			fpSetRtcTime = (PSETRTCTIME)GetProcAddress(LibHandle, "SetRtcTime");
			fpGetRtcTime = (PGETRTCTIME)GetProcAddress(LibHandle, "GetRtcTime");
			fpSetRtcAlarmTime = (PSETRTCALARMTIME)GetProcAddress(LibHandle, "SetRtcAlarmTime");
			fpGetRtcAlarmTime = (PGETRTCALARMTIME)GetProcAddress(LibHandle, "GetRtcAlarmTime");
			fpGetErrorCode = (PGETERRORCODE)GetProcAddress(LibHandle, "GetErrorCode");
			fpGetErrorCodeMessage = (PGETERRORCODEMESSAGE)GetProcAddress(LibHandle, "GetErrorCodeMessage");
			fpGetDeviceList = (PGETDEVICELIST)GetProcAddress(LibHandle, "GetDeviceList");
			fpFreeDeviceList = (PFREEDEVICELIST)GetProcAddress(LibHandle, "FreeDeviceList");
			fpStartCardFile = (PSTARTCARDFILE)GetProcAddress(LibHandle, "StartCardFile");
			fpStopCardFile = (PSTOPCARDFILE)GetProcAddress(LibHandle, "StopCardFile");
			fpGetCardFileSamples = (PGETCARDFILESAMPLES)GetProcAddress(LibHandle, "GetCardFileSamples");
			fpGetConnectionProperties = (PGETCONNECTIONPROPERTIES)GetProcAddress(LibHandle, "GetConnectionProperties");
			fpGetCardFileSignalFormat = (PGETCARDFILESIGNALFORMAT)GetProcAddress(LibHandle, "GetCardFileSignalFormat");
			fpOpenCardFile = (POPENCARDFILE)GetProcAddress(LibHandle, "OpenCardFile");
			fpGetCardFileList = (PGETCARDFILELIST)GetProcAddress(LibHandle, "GetCardFileList");
			fpCloseCardFile = (PCLOSECARDFILE)GetProcAddress(LibHandle, "CloseCardFile");
			fpGetExtFrontEndInfo = (PGETEXTFRONTENDINFO)GetProcAddress(LibHandle, "GetExtFrontEndInfo");
			fpSetMeasuringMode = (PSETMEASURINGMODE)GetProcAddress(LibHandle, "SetMeasuringMode");
			fpGetRecordingConfiguration = (PGETRECORDINGCONFIGURATION)GetProcAddress(LibHandle, "GetRecordingConfiguration");
			fpSetRecordingConfiguration = (PSETRECORDINGCONFIGURATION)GetProcAddress(LibHandle, "SetRecordingConfiguration");
			fpSetRefCalculation = (PSETREFCALCULATION)GetProcAddress(LibHandle, "SetRefCalculation");
			fpGetRandomKey = (PGETRANDOMKEY)GetProcAddress(LibHandle, "GetRandomKey");
			fpUnlockFrontEnd = (PUNLOCKFRONTEND)GetProcAddress(LibHandle, "UnlockFrontEnd");
			fpGetOEMSize = (PGETOEMSIZE)GetProcAddress(LibHandle, "GetOEMSize");
			fpGetOEMData = (PGETOEMDATA)GetProcAddress(LibHandle, "GetOEMData");
			fpSetOEMData = (PSETOEMDATA)GetProcAddress(LibHandle, "SetOEMData");
			fpOpenFirstDevice = (POPENFIRSTDEVICE)GetProcAddress(LibHandle, "OpenFirstDevice");
			fpSetStorageMode = (PSETSTORAGEMODE)GetProcAddress(LibHandle, "SetStorageMode");

			// Nexus4
			fpGetFlashStatus = (PGETFLASHSTATUS)GetProcAddress(LibHandle, "GetFlashStatus");
			fpStartFlashData = (PSTARTFLASHDATA)GetProcAddress(LibHandle, "StartFlashData");
			fpGetFlashSamples = (PGETFLASHSAMPLES)GetProcAddress(LibHandle, "GetFlashSamples");
			fpStopFlashData = (PSTOPFLASHDATA)GetProcAddress(LibHandle, "StopFlashData");
			fpFlashEraseMemory = (PFLASHERASEMEMORY)GetProcAddress(LibHandle, "FlashEraseMemory");
			fpSetFlashData = (PSETFLASHDATA)GetProcAddress(LibHandle, "SetFlashData");

			try
			{
				if (fpLibraryInit != nullptr)
					Handle = fpLibraryInit(TMSiConnectionUSB, &ErrorCode);
			}
			catch (AccessViolationException^ e)
			{
				//ErrorCode = fpGetErrorCode(Handle);
				throw gcnew Exception("TMSi Library wont Initialize...");
			}
			if (Handle)
			{
				DeviceList = fpGetDeviceList(Handle, &NrOfDevices);
			}
			else
			{
				//ErrorCode = fpGetErrorCode(Handle);
				throw gcnew Exception("TMSi found no Device...");
			}

			if (NrOfDevices == 0)
			{
				ErrorCode = fpGetErrorCode(Handle);
				throw gcnew Exception("Cannot find any device...");
			}
			char* DeviceLocator = DeviceList[0];
			Boolean Status = fpOpen(Handle, DeviceLocator);

			FRONTENDINFO FrontEndInfo;
			TMSiBatReportType TMSiBatReport;
			TMSiStorageReportType TMSiStorageReport;
			TMSiDeviceReportType TMSiDeviceReport;
			TMSiExtFrontendInfoType TMSiExtFrontEndInfo;

			Status = fpGetFrontEndInfo(Handle, &FrontEndInfo);
			Status = fpGetExtFrontEndInfo(Handle, &TMSiExtFrontEndInfo, &TMSiBatReport, &TMSiStorageReport, &TMSiDeviceReport);
		}

		catch (Exception^ e) {
			throw(e);
		}
	}


		  void InitiateImpedanceDataMode()
		  {
			  ULONG SampleRateInMilliHz;
			  int ErrorCode;
			  PSIGNAL_FORMAT psf = NULL;
			  char FrontEndName[MAX_FRONTENDNAME_LENGTH];
			  long BytesPerSample;
			  int NoDataRecieved = 0, Total = 0;


			  //		TRACEACTION("Try to get the SignalFormat");
			  try {
				  psf = fpGetSignalFormat(Handle, FrontEndName);
				  if (psf == NULL)
				  {
					  throw gcnew Exception("Cannot get SignalFormat");
					  ErrorCode = fpGetErrorCode(Handle);
				  }
			  }
			  catch (Exception^ e)
			  {
				  throw (e);
			  }

			  NrOfChannels = psf->Elements;
			  fpFree(psf);
			  psf = NULL;

			  BytesPerSample = NrOfChannels * sizeof(long);
			  if (BytesPerSample == 0)
			  {
				  return;
			  }

			  // Set the samplerate in Herz
			  SampleRateInMilliHz = 0;
			  SignalBufferSizeInSamples = 1000;
			  ULONG tmp = SignalBufferSizeInSamples;
			  //		TRACEACTION("Set the selected samplerate");
			  if (fpSetSignalBuffer(Handle, &SampleRateInMilliHz, &tmp) != TRUE)
			  {
				  ErrorCode = fpGetErrorCode(Handle);
				  throw gcnew Exception("Error in SetsignalBuffer");
				  return;
			  }

			  SignalBufferSizeInSamples = tmp;
			  SampleRateInHz = SampleRateInMilliHz / 1000;

			  // We know now the NumberOfChannels and the choosen SampleRateInHz, so allocate now the Signalbuffer
			  SignalBufferSizeInBytes = SignalBufferSizeInSamples * NrOfChannels * sizeof(SignalBuffer[0]);
			  SignalBuffer = (unsigned int*)malloc(SignalBufferSizeInBytes);

			  if (SignalBuffer == NULL)
			  {
				  throw gcnew Exception("No SignalBuffer Created");
			  }


			  NoDataRecieved = Total = 0;

			  if (!fpStart(Handle))
				  return;

			  // Turn on the impendance mode
			  if (!fpSetMeasuringMode(Handle, MEASURE_MODE_IMPEDANCE_EX, IC_OHM_200))
			  {
				  ErrorCode = fpGetErrorCode(Handle);
				  throw gcnew Exception("Cannot enter Impedance Mode");
				  return;
			  }
		  }
		  unsigned int* getImpedances()
		  {
			  unsigned int BytesReturned = fpGetSamples(Handle, (PULONG)SignalBuffer, SignalBufferSizeInBytes);
			  if (BytesReturned > 0)
				  return SignalBuffer;
			  else
				  return SignalBuffer;
				  //throw gcnew Exception("Cannot get data");
		  }
	};
}
