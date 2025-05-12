.code

extern TrAddr_GameLoopStart         : dq
extern TrAddr_RequestFile           : dq
extern TrAddr_ParseMaterial         : dq
extern TrAddr_MSFileSystemFile_dtor : dq

extern TrHandler_GameLoopStart          : proc
extern TrHandler_RequestFile            : proc
extern TrHandler_ParseMaterial          : proc
extern TrHandler_MSFileSystemFile_dtor  : proc

TrHook_GameLoopStart proc
    sub rsp, 20h
    call TrHandler_GameLoopStart
    add rsp, 20h
    jmp [TrAddr_GameLoopStart]
TrHook_GameLoopStart endp

TrHook_IsGameWindowActive proc
    mov al, 1
    ret
TrHook_IsGameWindowActive endp

TrHook_RequestFile proc
    mov rdx, rbx
    mov rcx, rbp
    sub rsp, 20h
    call TrHandler_RequestFile
    add rsp, 20h
    jmp [TrAddr_RequestFile]
TrHook_RequestFile endp

TrHook_ParseMaterial proc
    push rcx
    push rdx

    sub rsp, 20h
    mov ecx, r8d
    call TrHandler_ParseMaterial
    add rsp, 20h

    pop rdx
    pop rcx
    jmp [TrAddr_ParseMaterial]
TrHook_ParseMaterial endp

TrHook_MSFileSystemFile_dtor proc
    push rcx
    push rdx

    sub rsp, 20h
    call TrHandler_MSFileSystemFile_dtor
    add rsp, 20h

    pop rdx
    pop rcx
    jmp [TrAddr_MSFileSystemFile_dtor]
TrHook_MSFileSystemFile_dtor endp

END
