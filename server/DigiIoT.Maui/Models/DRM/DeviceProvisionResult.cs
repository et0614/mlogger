﻿/*
 * Copyright 2024, Digi International Inc.
 * 
 * Permission to use, copy, modify, and/or distribute this software for any
 * purpose with or without fee is hereby granted, provided that the above
 * copyright notice and this permission notice appear in all copies.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 * WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 * ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 * WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 * ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 * OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
 */

namespace DigiIoT.Maui.Models.DRM
{
    /// <summary>
    /// Represents the result of provisioning a Digi device.
    /// </summary>
    public class DeviceProvisionResult
    {
        // Properties.
        /// <summary>
        /// The ID of the device.
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// Whether the device provisioning was successful or not.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// The error message if provisioning failed (optional).
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The error code if provisioning failed (optional).
        /// </summary>
        public int? ErrorCode { get; set; }
    }
}
