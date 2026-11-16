#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <string>
// Windows 11 keeps the task process out of Explorer's job object with the desktop app
// breakaway policy. Older systems may reject that attribute, so launch without it there.
static BOOL createDetached(const std::wstring& executable,std::wstring& command,DWORD& error){
 SIZE_T bytes=0;InitializeProcThreadAttributeList(nullptr,1,0,&bytes);
 auto attributes=static_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(HeapAlloc(GetProcessHeap(),0,bytes));
 bool listReady=attributes&&InitializeProcThreadAttributeList(attributes,1,0,&bytes);
 DWORD policy=PROCESS_CREATION_DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE;
 bool policyReady=listReady&&UpdateProcThreadAttribute(attributes,0,PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY,&policy,sizeof(policy),nullptr,nullptr);
 STARTUPINFOEXW startup{};startup.StartupInfo.cb=policyReady?sizeof(STARTUPINFOEXW):sizeof(STARTUPINFOW);
 DWORD flags=0;
 if(policyReady){startup.lpAttributeList=attributes;flags=EXTENDED_STARTUPINFO_PRESENT;}
 PROCESS_INFORMATION process{};
 BOOL created=CreateProcessW(executable.c_str(),command.data(),nullptr,nullptr,FALSE,flags,nullptr,nullptr,&startup.StartupInfo,&process);
 error=created?ERROR_SUCCESS:GetLastError();
 if(listReady)DeleteProcThreadAttributeList(attributes);
 if(attributes)HeapFree(GetProcessHeap(),0,attributes);
 if(created){CloseHandle(process.hThread);CloseHandle(process.hProcess);}
 return created;
}
// Keep the Explorer COM server independent of the WinUI application's lifetime.
int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR,int){
 wchar_t path[32768];GetModuleFileNameW(nullptr,path,32768);PathRemoveFileSpecW(path);std::wstring executable=std::wstring(path)+L"\\New7z.App.exe",command=L"\""+executable+L"\"";
 int count=0;auto args=CommandLineToArgvW(GetCommandLineW(),&count);if(count==3&&std::wstring(args[1])==L"--shell-request")command+=L" --shell-request \""+std::wstring(args[2])+L"\"";LocalFree(args);
 DWORD error=ERROR_SUCCESS;
 return createDetached(executable,command,error)?0:int(error);
}
