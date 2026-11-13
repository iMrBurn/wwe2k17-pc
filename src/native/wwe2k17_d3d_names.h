// WWE 2K17 (Title Update 1.04 code, image 0.0.4.1) addresses for the XDK D3D functions that re:Blue's renderer hooks
// by name. Force-included into the native renderer sources, as wwe12_d3d_names.h is in WWE '12 PC.
//
// Taken from WWE 2K16 PC's header (TU3). Unlike 2K15 -> 2K16, the 2K17 library is not one constant shift away (only
// Present / Swap / SynchronizeToPresentationInterval are 2K16 - 0x80088): its functions were reordered. Every one of
// the 22 was found by instruction similarity with 2K16's (tools/match_functions.py, window 82B90000-82BD0000):
// all score 1.00 with the same instruction count, and the runner-up is well below except for the pairs told apart
// by length or by the exact 1.00 (Set/CreateVertexShader vs Set/CreatePixelShader 0.91-0.92, EndVertices 0.86,
// VertexBuffer_Unlock 0.86). The comment keeps 2K16's address.
#pragma once

#define WWE2K17_NATIVE 1

// Not identified in 2K15 yet: re:Blue compiles these hooks out.
#define SVR_NO_DRAW_VERTICES 1   // D3DDevice_DrawVertices
#define WWE12_NO_DESC_HOOKS 1    // the resource description getters

#define D3DDevice_Present                           sub_82BB2BF0  // 2K16 sub_82C32C78; never called in 2K16's census
#define D3DDevice_Swap                              sub_82BB2488  // 2K16 sub_82C32510; the only VdSwap caller
#define D3DDevice_SynchronizeToPresentationInterval sub_82BB1FC8  // 2K16 sub_82C32050
#define D3DDevice_Resolve                           sub_82BAAB70  // 2K16 sub_82C2E8D8; the public Resolve (SvR's arguments)
#define D3DDevice_Clear                             sub_82BB3F80  // 2K16 sub_82C34008
#define D3DDevice_SetRenderTarget                   sub_82BAFE68  // 2K16 sub_82C2AA48
#define D3DDevice_SetDepthStencilSurface            sub_82BB01F8  // 2K16 sub_82C2ADD8
#define D3DDevice_SetScissorRect                    sub_82BAF3F0  // 2K16 sub_82C29FD0
#define D3DDevice_BeginVertices                     sub_82BAC508  // 2K16 sub_82C30270
#define D3DDevice_EndVertices                       sub_82BAC4F8  // 2K16 sub_82C30260
#define D3DDevice_DrawIndexedVertices               sub_82BAC9C8  // 2K16 sub_82C30730; the hottest draw
#define D3DVertexBuffer_Unlock                      sub_82BA5C10  // 2K16 sub_82C2BBA0
#define D3DIndexBuffer_Unlock                       sub_82BA6DC0  // 2K16 sub_82C257F8
#define D3DDevice_CreateVertexDeclaration           sub_82BABEC0  // 2K16 sub_82C2FC28
#define D3DDevice_CreateVertexShader                sub_82BAC3B8  // 2K16 sub_82C30120
#define D3DDevice_CreatePixelShader                 sub_82BAC1D0  // 2K16 sub_82C2FF38
#define D3DDevice_SetTexture                        sub_82BA8E58  // 2K16 sub_82C27890
#define D3DDevice_SetVertexShader                   sub_82BABBA0  // 2K16 sub_82C2F908
#define D3DDevice_SetPixelShader                    sub_82BAB998  // 2K16 sub_82C2F700
#define D3DDevice_SetVertexDeclaration              sub_82BABDB8  // 2K16 sub_82C2FB20
#define D3DDevice_SetStreamSource                   sub_82BAF538  // 2K16 sub_82C2A118
#define D3DDevice_SetIndices                        sub_82BAF6E0  // 2K16 sub_82C2A2C0

// Original bodies (__imp__ = the recompiled function, for hooks that call through).
#define __imp__D3DDevice_Present                           __imp__sub_82BB2BF0
#define __imp__D3DDevice_Swap                              __imp__sub_82BB2488
#define __imp__D3DDevice_SynchronizeToPresentationInterval __imp__sub_82BB1FC8
#define __imp__D3DDevice_Resolve                           __imp__sub_82BAAB70
#define __imp__D3DDevice_Clear                             __imp__sub_82BB3F80
#define __imp__D3DDevice_SetRenderTarget                   __imp__sub_82BAFE68
#define __imp__D3DDevice_SetDepthStencilSurface            __imp__sub_82BB01F8
#define __imp__D3DDevice_SetScissorRect                    __imp__sub_82BAF3F0
#define __imp__D3DDevice_BeginVertices                     __imp__sub_82BAC508
#define __imp__D3DDevice_EndVertices                       __imp__sub_82BAC4F8
#define __imp__D3DDevice_DrawIndexedVertices               __imp__sub_82BAC9C8
#define __imp__D3DVertexBuffer_Unlock                      __imp__sub_82BA5C10
#define __imp__D3DIndexBuffer_Unlock                       __imp__sub_82BA6DC0
#define __imp__D3DDevice_CreateVertexDeclaration           __imp__sub_82BABEC0
#define __imp__D3DDevice_CreateVertexShader                __imp__sub_82BAC3B8
#define __imp__D3DDevice_CreatePixelShader                 __imp__sub_82BAC1D0
#define __imp__D3DDevice_SetTexture                        __imp__sub_82BA8E58
#define __imp__D3DDevice_SetVertexShader                   __imp__sub_82BABBA0
#define __imp__D3DDevice_SetPixelShader                    __imp__sub_82BAB998
#define __imp__D3DDevice_SetVertexDeclaration              __imp__sub_82BABDB8
#define __imp__D3DDevice_SetStreamSource                   __imp__sub_82BAF538
#define __imp__D3DDevice_SetIndices                        __imp__sub_82BAF6E0
