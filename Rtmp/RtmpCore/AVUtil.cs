
using MediaCommon;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace RtmpCore
{
    public static class AVUtil
    {
        private static int GetObjectType(ref BitReader reader)
        {
            var audioObjectType = reader.Read(5);
            if (audioObjectType == 31)
            {
                audioObjectType = reader.Read(6) + 32;
            }
            return (int) audioObjectType;
        }

        private static int GetSampleRate(ref BitReader reader)
        {
            var sampling_index = reader.Read(4);
            return sampling_index == 0x0f ? (int)reader.Read(24) : RtmpConstants.AacSampleRates[sampling_index];
        }

        public static object ReadAACSpecificConfig(ReadOnlySpan<byte> buffer)
        {
            dynamic info = new ExpandoObject();
            var reader = new BitReader(buffer);
            reader.Read(16);
            info.object_type = GetObjectType(ref reader);
            info.sample_rate = GetSampleRate(ref reader);
            info.chan_config = reader.Read(4);
            if (info.chan_config < RtmpConstants.AacChannels.Length)
            {
                info.channels = RtmpConstants.AacChannels[info.chan_config];
            }
            info.sbr = -1;
            info.ps = -1;
            if (info.object_type == 5 || info.object_type == 29)
            {
                if (info.object_type == 29)
                {
                    info.ps = 1;
                }
                info.ext_object_type = 5;
                info.sbr = 1;
                info.sample_rate = GetSampleRate(ref reader);
                info.object_type = GetObjectType(ref reader);
            }

            return info;
        }

        public static string GetAacProfileName(dynamic info)
        {
            switch (info.object_type)
            {
                case 1:
                    return "Main";
                case 2:
                    if (info.ps > 0)
                    {
                        return "HEv2";
                    }
                    if (info.sbr > 0)
                    {
                        return "HE";
                    }
                    return "LC";
                case 3:
                    return "SSR";
                case 4:
                    return "LTP";
                case 5:
                    return "SBR";
                default:
                    return "";
            }
        }

        public static object ReadAVCSpecificConfig(ReadOnlySpan<byte> buffer)
        {
            var codec_id = buffer[0] & 0x0f;
            if (codec_id == RtmpConstants.H264Video)
            {
                return ReadH264SpecificConfig(buffer);
            }
            else if (codec_id == RtmpConstants.H265Video)
            {
                return ReadHEVCSpecificConfig(buffer);
            }
            return null;
        }

        public static dynamic HEVCParsePtl(ref BitReader reader, int max_sub_layers_minus1)
        {
            dynamic general_ptl = new ExpandoObject();

            general_ptl.profile_space = reader.Read(2);
            general_ptl.tier_flag = reader.Read(1);
            general_ptl.profile_idc = reader.Read(5);
            general_ptl.profile_compatibility_flags = reader.Read(32);
            general_ptl.general_progressive_source_flag = reader.Read(1);
            general_ptl.general_interlaced_source_flag = reader.Read(1);
            general_ptl.general_non_packed_constraint_flag = reader.Read(1);
            general_ptl.general_frame_only_constraint_flag = reader.Read(1);
            reader.Read(32);
            reader.Read(12);
            general_ptl.level_idc = reader.Read(8);

            general_ptl.sub_layer_profile_present_flag = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_level_present_flag = new int[max_sub_layers_minus1];

            for (var i = 0; i < max_sub_layers_minus1; i++)
            {
                general_ptl.sub_layer_profile_present_flag[i] = reader.Read(1);
                general_ptl.sub_layer_level_present_flag[i] = reader.Read(1);
            }

            if (max_sub_layers_minus1 > 0)
            {
                for (var i = max_sub_layers_minus1; i < 8; i++)
                {
                    reader.Read(2);
                }
            }

            general_ptl.sub_layer_profile_space = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_tier_flag = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_profile_idc = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_profile_compatibility_flag = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_progressive_source_flag = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_interlaced_source_flag = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_non_packed_constraint_flag = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_frame_only_constraint_flag = new int[max_sub_layers_minus1];
            general_ptl.sub_layer_level_idc = new int[max_sub_layers_minus1];

            for (var i = 0; i < max_sub_layers_minus1; i++)
            {
                if (general_ptl.sub_layer_profile_present_flag[i])
                {
                    general_ptl.sub_layer_profile_space[i] = reader.Read(2);
                    general_ptl.sub_layer_tier_flag[i] = reader.Read(1);
                    general_ptl.sub_layer_profile_idc[i] = reader.Read(5);
                    general_ptl.sub_layer_profile_compatibility_flag[i] = reader.Read(32);
                    general_ptl.sub_layer_progressive_source_flag[i] = reader.Read(1);
                    general_ptl.sub_layer_interlaced_source_flag[i] = reader.Read(1);
                    general_ptl.sub_layer_non_packed_constraint_flag[i] = reader.Read(1);
                    general_ptl.sub_layer_frame_only_constraint_flag[i] = reader.Read(1);
                    reader.Read(32);
                    reader.Read(12);
                }
                if (general_ptl.sub_layer_level_present_flag[i])
                {
                    general_ptl.sub_layer_level_idc[i] = reader.Read(8);
                }
                else
                {
                    general_ptl.sub_layer_level_idc[i] = 1;
                }
            }
            return general_ptl;
        }

        public static string GetAVCProfileName(dynamic info)
        {
            switch (info.profile)
            {
                case 1:
                    return "Main";
                case 2:
                    return "Main 10";
                case 3:
                    return "Main Still Picture";
                case 66:
                    return "Baseline";
                case 77:
                    return "Main";
                case 100:
                    return "High";
                default:
                    return "";
            }
        }

        public static object HEVCParsesps(ReadOnlySpan<byte> sps, dynamic hevc)
        {
            dynamic psps = new ExpandoObject();
            var NumBytesInNALunit = sps.Length;
            var rbsp_array = new List<byte>();
            var reader = new BitReader(sps);

            reader.Read(1);//forbidden_zero_bit
            reader.Read(6);//nal_unit_type
            reader.Read(6);//nuh_reserved_zero_6bits
            reader.Read(3);//nuh_temporal_id_plus1

            for (var i = 2; i < NumBytesInNALunit; i++)
            {
                if (i + 2 < NumBytesInNALunit && reader.Look(24) == 0x000003)
                {
                    rbsp_array.Add((byte)reader.Read(8));
                    rbsp_array.Add((byte)reader.Read(8));
                    i += 2;
                    var emulation_prevention_three_byte = reader.Read(8); /* equal to 0x03 */
                }
                else
                {
                    rbsp_array.Add((byte)reader.Read(8));
                }
            }
            var rbsp = rbsp_array.ToArray();
            var rbspreader = new BitReader(rbsp);
            psps.sps_video_parameter_set_id = rbspreader.Read(4);
            psps.sps_max_sub_layers_minus1 = rbspreader.Read(3);
            psps.sps_temporal_id_nesting_flag = rbspreader.Read(1);
            psps.profile_tier_level = HEVCParsePtl(ref rbspreader, (int)psps.sps_max_sub_layers_minus1);
            psps.sps_seq_parameter_set_id = rbspreader.ReadGolomb();
            psps.chroma_format_idc = rbspreader.ReadGolomb();
            if (psps.chroma_format_idc == 3)
            {
                psps.separate_colour_plane_flag = rbspreader.Read(1);
            }
            else
            {
                psps.separate_colour_plane_flag = 0;
            }
            psps.pic_width_in_luma_samples = rbspreader.ReadGolomb();
            psps.pic_height_in_luma_samples = rbspreader.ReadGolomb();
            psps.conformance_window_flag = rbspreader.Read(1);
            if (psps.conformance_window_flag)
            {
                var vert_mult = 1 + (psps.chroma_format_idc < 2);
                var horiz_mult = 1 + (psps.chroma_format_idc < 3);
                psps.conf_win_left_offset = rbspreader.ReadGolomb() * horiz_mult;
                psps.conf_win_right_offset = rbspreader.ReadGolomb() * horiz_mult;
                psps.conf_win_top_offset = rbspreader.ReadGolomb() * vert_mult;
                psps.conf_win_bottom_offset = rbspreader.ReadGolomb() * vert_mult;
            }
            // Logger.debug(psps);
            return psps;
        }

        public static object ReadHEVCSpecificConfig(ReadOnlySpan<byte> buffer)
        {
            dynamic info = new ExpandoObject();
            info.width = 0;
            info.height = 0;
            info.profile = 0;
            info.level = 0;
            // var reader = new reader(buffer);
            // reader.Read(48);
            buffer = buffer.Slice(5);

            do
            {
                dynamic hevc = new ExpandoObject();
                if (buffer.Length < 23)
                {
                    break;
                }

                hevc.configurationVersion = buffer[0];
                if (hevc.configurationVersion != 1)
                {
                    break;
                }
                hevc.general_profile_space = (buffer[1] >> 6) & 0x03;
                hevc.general_tier_flag = (buffer[1] >> 5) & 0x01;
                hevc.general_profile_idc = buffer[1] & 0x1F;
                hevc.general_profile_compatibility_flags = (buffer[2] << 24) | (buffer[3] << 16) | (buffer[4] << 8) | buffer[5];
                hevc.general_constraint_indicator_flags = ((buffer[6] << 24) | (buffer[7] << 16) | (buffer[8] << 8) | buffer[9]);
                hevc.general_constraint_indicator_flags = (hevc.general_constraint_indicator_flags << 16) | (buffer[10] << 8) | buffer[11];
                hevc.general_level_idc = buffer[12];
                hevc.min_spatial_segmentation_idc = ((buffer[13] & 0x0F) << 8) | buffer[14];
                hevc.parallelismType = buffer[15] & 0x03;
                hevc.chromaFormat = buffer[16] & 0x03;
                hevc.bitDepthLumaMinus8 = buffer[17] & 0x07;
                hevc.bitDepthChromaMinus8 = buffer[18] & 0x07;
                hevc.avgFrameRate = (buffer[19] << 8) | buffer[20];
                hevc.constantFrameRate = (buffer[21] >> 6) & 0x03;
                hevc.numTemporalLayers = (buffer[21] >> 3) & 0x07;
                hevc.temporalIdNested = (buffer[21] >> 2) & 0x01;
                hevc.LengthSizeMinusOne = buffer[21] & 0x03;
                var numOfArrays = buffer[22];
                var p = buffer.Slice(23);
                for (var i = 0; i < numOfArrays; i++)
                {
                    if (p.Length < 3)
                    {
                        break;
                    }
                    var nalutype = p[0];
                    var n = (p[1]) << 8 | p[2];
                    // Logger.debug(nalutype, n);
                    p = p.Slice(3);
                    for (var j = 0; j < n; j++)
                    {
                        if (p.Length < 2)
                        {
                            break;
                        }
                        var k = (p[0] << 8) | p[1];
                        // Logger.debug("k", k);
                        if (p.Length < 2 + k)
                        {
                            break;
                        }
                        p = p.Slice(2);
                        if (nalutype == 33)
                        {
                            //sps
                            var sps = new byte[k];
                            p.Slice(k).CopyTo(sps);
                            // Logger.debug(sps, sps.Length);
                            hevc.psps = HEVCParsesps(sps, hevc);
                            info.profile = hevc.general_profile_idc;
                            info.level = hevc.general_level_idc / 30.0;
                            info.width = hevc.psps.pic_width_in_luma_samples - (hevc.psps.conf_win_left_offset + hevc.psps.conf_win_right_offset);
                            info.height = hevc.psps.pic_height_in_luma_samples - (hevc.psps.conf_win_top_offset + hevc.psps.conf_win_bottom_offset);
                        }
                        p = p.Slice(k);
                    }
                }
            } while (false);

            return info;
        }

        public static object ReadH264SpecificConfig(ReadOnlySpan<byte> buffer)
        {
            dynamic info = new ExpandoObject();
            int profile_idc, width, height, crop_left, crop_right,
              crop_top, crop_bottom, frame_mbs_only, n, cf_idc,
              num_ref_frames;
            var reader = new BitReader(buffer);
            reader.ReadLong(48);
            info.width = 0;
            info.height = 0;

            do
            {
                info.profile = reader.Read(8);
                info.compat = reader.Read(8);
                info.level = reader.Read(8);
                info.nalu = (reader.Read(8) & 0x03) + 1;
                info.nb_sps = reader.Read(8) & 0x1F;
                if (info.nb_sps == 0)
                {
                    break;
                }
                /* nal size */
                reader.Read(16);

                /* nal type */
                if (reader.Read(8) != 0x67)
                {
                    break;
                }
                /* sps */
                profile_idc = reader.Read(8);

                /* flags */
                reader.Read(8);

                /* level idc */
                reader.Read(8);

                /* sps id */
                reader.ReadGolomb();

                if (profile_idc == 100 || profile_idc == 110 ||
                  profile_idc == 122 || profile_idc == 244 || profile_idc == 44 ||
                  profile_idc == 83 || profile_idc == 86 || profile_idc == 118)
                {
                    /* chroma format idc */
                    cf_idc = reader.ReadGolomb();

                    if (cf_idc == 3)
                    {

                        /* separate color plane */
                        reader.Read(1);
                    }

                    /* bit depth luma - 8 */
                    reader.ReadGolomb();

                    /* bit depth chroma - 8 */
                    reader.ReadGolomb();

                    /* qpprime y zero transform bypass */
                    reader.Read(1);

                    /* seq scaling matrix present */
                    if (reader.Read(1) == 1)
                    {

                        for (n = 0; n < (cf_idc != 3 ? 8 : 12); n++)
                        {

                            /* seq scaling list present */
                            if (reader.Read(1) == 1)
                            {

                                /* TODO: scaling_list()
                                if (n < 6) {
                                } else {
                                }
                                */
                            }
                        }
                    }
                }

                /* log2 max frame num */
                reader.ReadGolomb();

                /* pic order cnt type */
                switch (reader.ReadGolomb())
                {
                    case 0:

                        /* max pic order cnt */
                        reader.ReadGolomb();
                        break;

                    case 1:

                        /* delta pic order alwys zero */
                        reader.Read(1);

                        /* offset for non-ref pic */
                        reader.ReadGolomb();

                        /* offset for top to bottom field */
                        reader.ReadGolomb();

                        /* num ref frames in pic order */
                        num_ref_frames = reader.ReadGolomb();

                        for (n = 0; n < num_ref_frames; n++)
                        {

                            /* offset for ref frame */
                            reader.ReadGolomb();
                        }
                        break;
                }

                /* num ref frames */
                info.avc_ref_frames = reader.ReadGolomb();

                /* gaps in frame num allowed */
                reader.Read(1);

                /* pic width in mbs - 1 */
                width = reader.ReadGolomb();

                /* pic height in map units - 1 */
                height = reader.ReadGolomb();

                /* frame mbs only flag */
                frame_mbs_only = reader.Read(1);

                if (frame_mbs_only == 0)
                {

                    /* mbs adaprive frame field */
                    reader.Read(1);
                }

                /* direct 8x8 inference flag */
                reader.Read(1);

                /* frame cropping */
                if (reader.Read(1) == 0)
                {

                    crop_left = reader.ReadGolomb();
                    crop_right = reader.ReadGolomb();
                    crop_top = reader.ReadGolomb();
                    crop_bottom = reader.ReadGolomb();

                }
                else
                {
                    crop_left = 0;
                    crop_right = 0;
                    crop_top = 0;
                    crop_bottom = 0;
                }
                info.level = info.level / 10.0;
                info.width = (width + 1) * 16 - (crop_left + crop_right) * 2;
                info.height = (2 - frame_mbs_only) * (height + 1) * 16 - (crop_top + crop_bottom) * 2;

            } while (false);

            return info;
        }
    }
}
