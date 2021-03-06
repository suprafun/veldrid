﻿using System.Collections.Generic;
using Vulkan;
using static Vulkan.VulkanNative;
using static Veldrid.Vk.VulkanUtil;
using System;
using System.Diagnostics;

namespace Veldrid.Vk
{
    internal unsafe class VkFramebuffer : VkFramebufferBase
    {
        private readonly VkGraphicsDevice _gd;
        private readonly Vulkan.VkFramebuffer _deviceFramebuffer;
        private readonly VkRenderPass _renderPass;
        private readonly List<VkImageView> _attachmentViews = new List<VkImageView>();

        public override Vulkan.VkFramebuffer CurrentFramebuffer => _deviceFramebuffer;
        public override VkRenderPass RenderPass => _renderPass;

        public VkFramebuffer(VkGraphicsDevice gd, ref FramebufferDescription description)
            : base(description.DepthTarget, description.ColorTargets)
        {
            _gd = gd;

            VkRenderPassCreateInfo renderPassCI = VkRenderPassCreateInfo.New();

            StackList<VkAttachmentDescription> attachments = new StackList<VkAttachmentDescription>();

            uint colorAttachmentCount = (uint)ColorTextures.Count;
            StackList<VkAttachmentDescription> colorAttachmentDescriptions = new StackList<VkAttachmentDescription>();
            StackList<VkAttachmentReference> colorAttachmentRefs = new StackList<VkAttachmentReference>();
            for (int i = 0; i < colorAttachmentCount; i++)
            {
                VkTexture vkColorTex = Util.AssertSubtype<Texture, VkTexture>(ColorTextures[i]);
                VkAttachmentDescription colorAttachmentDesc = new VkAttachmentDescription();
                colorAttachmentDesc.format = vkColorTex.VkFormat;
                colorAttachmentDesc.samples = VkSampleCountFlags.Count1;
                colorAttachmentDesc.loadOp = VkAttachmentLoadOp.DontCare;
                colorAttachmentDesc.storeOp = VkAttachmentStoreOp.Store;
                colorAttachmentDesc.stencilLoadOp = VkAttachmentLoadOp.DontCare;
                colorAttachmentDesc.stencilStoreOp = VkAttachmentStoreOp.DontCare;
                colorAttachmentDesc.initialLayout = VkImageLayout.Undefined;
                colorAttachmentDesc.finalLayout = VkImageLayout.PresentSrcKHR;
                colorAttachmentDescriptions.Add(colorAttachmentDesc);
                attachments.Add(colorAttachmentDesc);

                VkAttachmentReference colorAttachmentRef = new VkAttachmentReference();
                colorAttachmentRef.attachment = (uint)i;
                colorAttachmentRef.layout = VkImageLayout.ColorAttachmentOptimal;
                colorAttachmentRefs.Add(colorAttachmentRef);
            }

            VkTexture vkDepthTex = Util.AssertSubtype<Texture, VkTexture>(DepthTexture);
            VkAttachmentDescription depthAttachmentDesc = new VkAttachmentDescription();
            VkAttachmentReference depthAttachmentRef = new VkAttachmentReference();
            if (vkDepthTex != null)
            {
                depthAttachmentDesc.format = vkDepthTex.VkFormat;
                depthAttachmentDesc.samples = VkSampleCountFlags.Count1;
                depthAttachmentDesc.loadOp = VkAttachmentLoadOp.DontCare;
                depthAttachmentDesc.storeOp = VkAttachmentStoreOp.Store;
                depthAttachmentDesc.stencilLoadOp = VkAttachmentLoadOp.DontCare;
                depthAttachmentDesc.stencilStoreOp = VkAttachmentStoreOp.DontCare;
                depthAttachmentDesc.initialLayout = VkImageLayout.Undefined;
                depthAttachmentDesc.finalLayout = VkImageLayout.ShaderReadOnlyOptimal;

                depthAttachmentRef.attachment = (uint)description.ColorTargets.Length;
                depthAttachmentRef.layout = VkImageLayout.DepthStencilAttachmentOptimal;
            }

            VkSubpassDescription subpass = new VkSubpassDescription();
            subpass.pipelineBindPoint = VkPipelineBindPoint.Graphics;
            if (ColorTextures.Count > 0)
            {
                subpass.colorAttachmentCount = colorAttachmentCount;
                subpass.pColorAttachments = (VkAttachmentReference*)colorAttachmentRefs.Data;
            }

            if (DepthTexture != null)
            {
                subpass.pDepthStencilAttachment = &depthAttachmentRef;
                attachments.Add(depthAttachmentDesc);
            }

            VkSubpassDependency subpassDependency = new VkSubpassDependency();
            subpassDependency.srcSubpass = SubpassExternal;
            subpassDependency.srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput;
            subpassDependency.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput;
            subpassDependency.dstAccessMask = VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite;
            if (DepthTexture != null)
            {
                subpassDependency.dstAccessMask |= VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite;
            }

            renderPassCI.attachmentCount = attachments.Count;
            renderPassCI.pAttachments = (VkAttachmentDescription*)attachments.Data;
            renderPassCI.subpassCount = 1;
            renderPassCI.pSubpasses = &subpass;
            renderPassCI.dependencyCount = 1;
            renderPassCI.pDependencies = &subpassDependency;

            VkResult creationResult = vkCreateRenderPass(_gd.Device, ref renderPassCI, null, out _renderPass);
            CheckResult(creationResult);

            VkFramebufferCreateInfo fbCI = VkFramebufferCreateInfo.New();
            uint fbAttachmentsCount = (uint)description.ColorTargets.Length;
            if (description.DepthTarget != null)
            {
                fbAttachmentsCount += 1;
            }

            VkImageView* fbAttachments = stackalloc VkImageView[(int)fbAttachmentsCount];
            for (int i = 0; i < fbAttachmentsCount - 1; i++)
            {
                Texture colorTarget = description.ColorTargets[i];
                VkTexture vkColorTarget = Util.AssertSubtype<Texture, VkTexture>(colorTarget);
                VkImageViewCreateInfo imageViewCI = VkImageViewCreateInfo.New();
                imageViewCI.image = vkColorTarget.DeviceImage;
                imageViewCI.format = vkColorTarget.VkFormat;
                imageViewCI.viewType = VkImageViewType.Image2D;
                imageViewCI.subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1);
                VkImageView* dest = (fbAttachments + i);
                VkResult result = vkCreateImageView(_gd.Device, ref imageViewCI, null, dest);
                CheckResult(result);
                _attachmentViews.Add(*dest);
            }

            // Depth
            if (description.DepthTarget != null)
            {
                VkTexture vkDepthTarget = Util.AssertSubtype<Texture, VkTexture>(description.DepthTarget);
                VkImageViewCreateInfo depthViewCI = VkImageViewCreateInfo.New();
                depthViewCI.image = vkDepthTarget.DeviceImage;
                depthViewCI.format = vkDepthTarget.VkFormat;
                depthViewCI.viewType = VkImageViewType.Image2D;
                depthViewCI.subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, 1);
                VkImageView* dest = (fbAttachments + (fbAttachmentsCount - 1));
                VkResult result = vkCreateImageView(_gd.Device, ref depthViewCI, null, dest);
                CheckResult(result);
                _attachmentViews.Add(*dest);
            }

            if (ColorTextures.Count > 0)
            {
                fbCI.width= ColorTextures[0].Width;
                fbCI.height = ColorTextures[0].Height;
            }
            else if (vkDepthTex != null)
            {
                fbCI.width = vkDepthTex.Width;
                fbCI.height = vkDepthTex.Height;
            }

            fbCI.attachmentCount = fbAttachmentsCount;
            fbCI.pAttachments = fbAttachments;
            fbCI.layers = 1;
            fbCI.renderPass = _renderPass;

            creationResult = vkCreateFramebuffer(_gd.Device, ref fbCI, null, out _deviceFramebuffer);
            CheckResult(creationResult);
        }

        public override void Dispose()
        {
            vkDestroyFramebuffer(_gd.Device, _deviceFramebuffer, null);
            vkDestroyRenderPass(_gd.Device, _renderPass, null);
            foreach (VkImageView view in _attachmentViews)
            {
                vkDestroyImageView(_gd.Device, view, null);
            }
        }
    }
}
