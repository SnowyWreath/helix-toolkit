﻿/*
The MIT License (MIT)
Copyright (c) 2018 Helix Toolkit contributors
*/
using SharpDX;

#if !NETFX_CORE
namespace HelixToolkit.Wpf.SharpDX
#else
#if CORE
namespace HelixToolkit.SharpDX.Core
#else
namespace HelixToolkit.UWP
#endif
#endif
{
    namespace Core
    {
        using Render;
        using Shaders;

        public class BoneSkinRenderCore : MeshRenderCore
        {
            //TODO: not doing this on mtweights unless we see its necessary (it might be)
            private bool matricsChanged = true;
            public Matrix[] BoneMatrices
            {
                set
                {
                    internalBoneBuffer.BoneMatrices = value;
                }
                get { return internalBoneBuffer.BoneMatrices; }
            }

            public float[] MorphTargetWeights
            {
                get { return internalMTBuffer.MorphTargetWeights; }
                set { internalMTBuffer.MorphTargetWeights = value; }
            }

            private BoneUploaderCore sharedBoneBuffer;
            public BoneUploaderCore SharedBoneBuffer
            {
                set
                {
                    var old = sharedBoneBuffer;
                    if(Set(ref sharedBoneBuffer, value))
                    {
                        if (old != null)
                        {
                            old.BoneChanged -= OnBoneChanged;
                        }
                        if (value != null)
                        {
                            value.BoneChanged += OnBoneChanged;
                        }
                        matricsChanged = true;
                    }
                }
                get { return sharedBoneBuffer; }
            }

            private int boneSkinSBSlot;
            private int mtWeightsBSlot;
            private ShaderPass preComputeBoneSkinPass;
            private IBoneSkinPreComputehBufferModel preComputeBoneBuffer;
            private readonly BoneUploaderCore internalBoneBuffer = new BoneUploaderCore();
            private readonly MorphTargetUploaderCore internalMTBuffer = new MorphTargetUploaderCore();

            public BoneSkinRenderCore()
            {
                NeedUpdate = true;
                internalBoneBuffer.BoneChanged += OnBoneChanged;
            }

            protected override bool OnAttach(IRenderTechnique technique)
            {
                if(base.OnAttach(technique))
                {
                    matricsChanged = true;
                    preComputeBoneSkinPass = technique[DefaultPassNames.PreComputeMeshBoneSkinned];
                    boneSkinSBSlot = preComputeBoneSkinPass.VertexShader.ShaderResourceViewMapping.GetMapping(DefaultBufferNames.BoneSkinSB).Slot;
                    mtWeightsBSlot = preComputeBoneSkinPass.VertexShader.ShaderResourceViewMapping.GetMapping(DefaultBufferNames.MTWeightsB).Slot;
                    internalBoneBuffer.Attach(technique);
                    internalMTBuffer.Attach(technique);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            private void OnBoneChanged(object sender, System.EventArgs e)
            {
                matricsChanged = true;
            }

            protected override void OnGeometryBufferChanged(IAttachableBufferModel buffer)
            {
                base.OnGeometryBufferChanged(buffer);
                preComputeBoneBuffer = buffer as IBoneSkinPreComputehBufferModel;
            }

            protected override void OnUpdate(RenderContext context, DeviceContextProxy deviceContext)
            {
                if (preComputeBoneSkinPass.IsNULL || preComputeBoneBuffer == null || !preComputeBoneBuffer.CanPreCompute || !matricsChanged)
                {
                    return;
                }
                var boneBuffer = sharedBoneBuffer ?? internalBoneBuffer;

                if(boneBuffer.BoneMatrices.Length == 0)
                {
                    preComputeBoneBuffer.ResetSkinnedVertexBuffer(deviceContext);
                }
                else
                {
                    GeometryBuffer.UpdateBuffers(deviceContext, EffectTechnique.EffectsManager);
                    preComputeBoneBuffer.BindSkinnedVertexBufferToOutput(deviceContext);
                    boneBuffer.Update(context, deviceContext);
                    internalMTBuffer.Update(context, deviceContext);
                    preComputeBoneSkinPass.BindShader(deviceContext);
                    boneBuffer.BindBuffer(deviceContext, boneSkinSBSlot);
                    internalMTBuffer.BindBuffers(deviceContext, mtWeightsBSlot);
                    deviceContext.Draw(GeometryBuffer.VertexBuffer[0].ElementCount, 0);
                    preComputeBoneBuffer.UnBindSkinnedVertexBufferToOutput(deviceContext);
                }
                matricsChanged = false;         
            }

            protected override void OnDetach()
            {
                preComputeBoneBuffer = null;
                internalBoneBuffer.Detach();
                internalMTBuffer.Detach();
                base.OnDetach();
            }

            public int CopySkinnedToArray(DeviceContextProxy context, Vector3[] array)
            {
                return preComputeBoneBuffer.CopySkinnedToArray(context, array);
            }
        }
    }

}
