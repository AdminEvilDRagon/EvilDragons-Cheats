; generated - jump stubs to the real dxgi.dll
PUBLIC g_real
.data
g_real QWORD 20 DUP(0)
.code
ED_ApplyCompatResolutionQuirking PROC
  jmp QWORD PTR [g_real+0]
ED_ApplyCompatResolutionQuirking ENDP
ED_CompatString PROC
  jmp QWORD PTR [g_real+8]
ED_CompatString ENDP
ED_CompatValue PROC
  jmp QWORD PTR [g_real+16]
ED_CompatValue ENDP
ED_CreateDXGIFactory PROC
  jmp QWORD PTR [g_real+24]
ED_CreateDXGIFactory ENDP
ED_CreateDXGIFactory1 PROC
  jmp QWORD PTR [g_real+32]
ED_CreateDXGIFactory1 ENDP
ED_CreateDXGIFactory2 PROC
  jmp QWORD PTR [g_real+40]
ED_CreateDXGIFactory2 ENDP
ED_DXGID3D10CreateDevice PROC
  jmp QWORD PTR [g_real+48]
ED_DXGID3D10CreateDevice ENDP
ED_DXGID3D10CreateLayeredDevice PROC
  jmp QWORD PTR [g_real+56]
ED_DXGID3D10CreateLayeredDevice ENDP
ED_DXGID3D10GetLayeredDeviceSize PROC
  jmp QWORD PTR [g_real+64]
ED_DXGID3D10GetLayeredDeviceSize ENDP
ED_DXGID3D10RegisterLayers PROC
  jmp QWORD PTR [g_real+72]
ED_DXGID3D10RegisterLayers ENDP
ED_DXGIDeclareAdapterRemovalSupport PROC
  jmp QWORD PTR [g_real+80]
ED_DXGIDeclareAdapterRemovalSupport ENDP
ED_DXGIDisableVBlankVirtualization PROC
  jmp QWORD PTR [g_real+88]
ED_DXGIDisableVBlankVirtualization ENDP
ED_DXGIDumpJournal PROC
  jmp QWORD PTR [g_real+96]
ED_DXGIDumpJournal ENDP
ED_DXGIGetDebugInterface1 PROC
  jmp QWORD PTR [g_real+104]
ED_DXGIGetDebugInterface1 ENDP
ED_DXGIReportAdapterConfiguration PROC
  jmp QWORD PTR [g_real+112]
ED_DXGIReportAdapterConfiguration ENDP
ED_PIXBeginCapture PROC
  jmp QWORD PTR [g_real+120]
ED_PIXBeginCapture ENDP
ED_PIXEndCapture PROC
  jmp QWORD PTR [g_real+128]
ED_PIXEndCapture ENDP
ED_PIXGetCaptureState PROC
  jmp QWORD PTR [g_real+136]
ED_PIXGetCaptureState ENDP
ED_SetAppCompatStringPointer PROC
  jmp QWORD PTR [g_real+144]
ED_SetAppCompatStringPointer ENDP
ED_UpdateHMDEmulationStatus PROC
  jmp QWORD PTR [g_real+152]
ED_UpdateHMDEmulationStatus ENDP
END
