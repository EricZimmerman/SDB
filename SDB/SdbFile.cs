using System;
using System.Collections.Generic;
using System.IO;
using SDB.EntryTypes;
using Serilog;

namespace SDB;

public class SdbFile
{
    public enum TagType : ushort
    {
        TAG_TYPE_NULL = 0x1000,
        TAG_TYPE_BYTE = 0x2000,
        TAG_TYPE_WORD = 0x3000,
        TAG_TYPE_DWORD = 0x4000,
        TAG_TYPE_QWORD = 0x5000,
        TAG_TYPE_STRINGREF = 0x6000,
        TAG_TYPE_LIST = 0x7000,
        TAG_TYPE_STRING = 0x8000,
        TAG_TYPE_BINARY = 0x9000
    }

    public enum TagValue : ushort
    {
        TAG_INCLUDE = 0x1001,
        TAG_GENERAL = 0x1002,
        TAG_MATCH_LOGIC_NOT = 0x1003,
        TAG_APPLY_ALL_SHIMS = 0x1004,
        TAG_USE_SERVICE_PACK_FILES = 0x1005,
        TAG_MITIGATION_OS = 0x1006,
        TAG_BLOCK_UPGRADE_OR_TAG_TRACE_PCA = 0x1007,
        TAG_INCLUDEEXCLUDEDLL = 0x1008,
        TAG_RAC_EVENT_OFF = 0x1009,
        TAG_TELEMETRY_OFF = 0x100A,
        TAG_SHIM_ENGINE_OFF = 0x100B,
        TAG_LAYER_PROPAGATION_OFF = 0x100C,
        TAG_REINSTALL_UPGRADE_OR_TAG_FORCE_CACHE = 0x100D,
        TAG_MONITORING_OFF = 0x100E,
        TAG_QUIRK_OFF = 0x100F,
        TAG_ELEVATED_PROP_OFF = 0x1010,
        TAG_UPGRADE_ACTION_BLOCK_WEBSETUP = 0x1011,
        TAG_UPGRADE_ACTION_PROCEED_TO_MEDIASETUP = 0x1012,

        TAG_MATCH_MODE = 0x3001,
        TAG_QUIRK_COMPONENT_CODE_ID = 0x3002,
        TAG_QUIRK_CODE_ID = 0x3003,

        TAG_TAG = 0x3801,
        TAG_INDEX_TAG = 0x3802,
        TAG_INDEX_KEY = 0x3803,

        TAG_SIZE = 0x4001,
        TAG_OFFSET = 0x4002,
        TAG_CHECKSUM = 0x4003,
        TAG_SHIM_TAGID = 0x4004,
        TAG_PATCH_TAGID = 0x4005,
        TAG_MODULE_TYPE = 0x4006,
        TAG_VERDATEHI = 0x4007,
        TAG_VERDATELO = 0x4008,
        TAG_VERFILEOS = 0x4009,
        TAG_VERFILETYPE = 0x400A,
        TAG_PE_CHECKSUM = 0x400B,
        TAG_PREVOSMAJORVER = 0x400C,
        TAG_PREVOSMINORVER = 0x400D,
        TAG_PREVOSPLATFORMID = 0x400E,
        TAG_PREVOSBUILDNO = 0x400F,
        TAG_PROBLEMSEVERITY = 0x4010,
        TAG_LANGID = 0x4011,
        TAG_VER_LANGUAGE = 0x4012,
        TAG_ENGINE = 0x4014,
        TAG_HTMLHELPID = 0x4015,
        TAG_INDEX_FLAGS = 0x4016,
        TAG_FLAGS = 0x4017,
        TAG_DATA_VALUETYPE = 0x4018,
        TAG_DATA_DWORD = 0x4019,
        TAG_LAYER_TAGID = 0x401A,
        TAG_MSI_TRANSFORM_TAGID = 0x401B,
        TAG_LINKER_VERSION = 0x401C,
        TAG_LINK_DATE = 0x401D,
        TAG_UPTO_LINK_DATE = 0x401E,
        TAG_OS_SERVICE_PACK = 0x401F,
        TAG_FLAG_TAGID = 0x4020,
        TAG_RUNTIME_PLATFORM = 0x4021,
        TAG_OS_SKU = 0x4022,
        TAG_OS_PLATFORM_OR_TAG_DEPRECATED_OS_PLATFORM = 0x4023,
        TAG_APP_NAME_RC_ID = 0x4024,
        TAG_VENDOR_NAME_RC_ID = 0x4025,
        TAG_SUMMARY_MSG_RC_ID = 0x4026,
        TAG_VISTA_SKU = 0x4027,
        TAG_DESCRIPTION_RC_ID = 0x4028,
        TAG_PARAMETER1_RC_ID = 0x4029,
        TAG_CONTEXT_TAGID = 0x4030,
        TAG_EXE_WRAPPER = 0x4031,
        TAG_URL_ID_OR_TAG_EXE_TYPE = 0x4032,
        TAG_FROM_LINK_DATE = 0x4033,
        TAG_URL_ID_OR_TAG_REVISION_EQ = 0x4034,
        TAG_REVISION_LE = 0x4035,
        TAG_REVISION_GE = 0x4036,
        TAG_DATE_EQ = 0x4037,
        TAG_DATE_LE = 0x4038,
        TAG_DATE_GE = 0x4039,
        TAG_CPU_MODEL_EQ = 0x403A,
        TAG_CPU_MODEL_LE = 0x403B,
        TAG_CPU_MODEL_GE = 0x403C,
        TAG_CPU_FAMILY_EQ = 0x403D,
        TAG_CPU_FAMILY_LE = 0x403E,
        TAG_CPU_FAMILY_GE = 0x403F,
        TAG_CREATOR_REVISION_EQ = 0x4040,
        TAG_CREATOR_REVISION_LE = 0x4041,
        TAG_CREATOR_REVISION_GE = 0x4042,
        TAG_SIZE_OF_IMAGE = 0x4043,
        TAG_SHIM_CLASS = 0x4044,
        TAG_PACKAGEID_ARCHITECTURE = 0x4045,
        TAG_REINSTALL_UPGRADE_TYPE = 0x4046,
        TAG_BLOCK_UPGRADE_TYPE = 0x4047,
        TAG_ROUTING_MODE = 0x4048,
        TAG_OS_VERSION_VALUE = 0x4049,
        TAG_CRC_CHECKSUM = 0x404A,
        TAG_URL_ID = 0x404B,
        TAG_QUIRK_TAGID = 0x404C,
        TAG_MIGRATION_DATA_TYPE = 0x404E,
        TAG_UPGRADE_DATA = 0x404F,
        TAG_MIGRATION_DATA_TAGID = 0x4050,
        TAG_REG_VALUE_TYPE = 0x4051,
        TAG_REG_VALUE_DATA_DWORD = 0x4052,
        TAG_TEXT_ENCODING = 0x4053,
        TAG_UX_BLOCKTYPE_OVERRIDE = 0x4054,
        TAG_EDITION = 0x4055,
        TAG_FW_LINK_ID = 0x4056,
        TAG_KB_ARTICLE_ID = 0x4057,
        TAG_UPGRADE_MODE = 0x4058,

        TAG_TAGID = 0x4801,

        TAG_TIME = 0x5001,
        TAG_BIN_FILE_VERSION = 0x5002,
        TAG_BIN_PRODUCT_VERSION = 0x5003,
        TAG_MODTIME = 0x5004,
        TAG_FLAG_MASK_KERNEL = 0x5005,
        TAG_UPTO_BIN_PRODUCT_VERSION = 0x5006,
        TAG_DATA_QWORD = 0x5007,
        TAG_FLAG_MASK_USER = 0x5008,
        TAG_FLAGS_NTVDM1 = 0x5009,
        TAG_FLAGS_NTVDM2 = 0x500A,
        TAG_FLAGS_NTVDM3 = 0x500B,
        TAG_FLAG_MASK_SHELL = 0x500C,
        TAG_UPTO_BIN_FILE_VERSION = 0x500D,
        TAG_FLAG_MASK_FUSION = 0x500E,
        TAG_FLAG_PROCESSPARAM = 0x500F,
        TAG_FLAG_LUA = 0x5010,
        TAG_FLAG_INSTALL = 0x5011,
        TAG_FROM_BIN_PRODUCT_VERSION = 0x5012,
        TAG_FROM_BIN_FILE_VERSION = 0x5013,
        TAG_PACKAGEID_VERSION = 0x5014,
        TAG_FROM_PACKAGEID_VERSION = 0x5015,
        TAG_UPTO_PACKAGEID_VERSION = 0x5016,
        TAG_OSMAXVERSIONTESTED = 0x5017,
        TAG_FROM_OSMAXVERSIONTESTED = 0x5018,
        TAG_UPTO_OSMAXVERSIONTESTED = 0x5019,
        TAG_FLAG_MASK_WINRT = 0x501A,
        TAG_REG_VALUE_DATA_QWORD = 0x501B,
        TAG_QUIRK_ENABLED_UPTO_VERSION_OR_TAG_QUIRK_ENABLED_VERSION_LT = 0x501C,
        TAG_SOURCE_OS = 0x501D,
        TAG_SOURCE_OS_LTE = 0x501E,
        TAG_SOURCE_OS_GTE = 0x501F,


        TAG_NAME = 0x6001,
        TAG_DESCRIPTION = 0x6002,
        TAG_MODULE = 0x6003,
        TAG_API = 0x6004,
        TAG_VENDOR = 0x6005,
        TAG_APP_NAME = 0x6006,
        TAG_COMMAND_LINE = 0x6008,
        TAG_COMPANY_NAME = 0x6009,
        TAG_DLLFILE = 0x600A,
        TAG_WILDCARD_NAME = 0x600B,
        TAG_PRODUCT_NAME = 0x6010,
        TAG_PRODUCT_VERSION = 0x6011,
        TAG_FILE_DESCRIPTION = 0x6012,
        TAG_FILE_VERSION = 0x6013,
        TAG_ORIGINAL_FILENAME = 0x6014,
        TAG_INTERNAL_NAME = 0x6015,
        TAG_LEGAL_COPYRIGHT = 0x6016,
        TAG_16BIT_DESCRIPTION = 0x6017,
        TAG_APPHELP_DETAILS = 0x6018,
        TAG_LINK_URL = 0x6019,
        TAG_LINK_TEXT = 0x601A,
        TAG_APPHELP_TITLE = 0x601B,
        TAG_APPHELP_CONTACT = 0x601C,
        TAG_SXS_MANIFEST = 0x601D,
        TAG_DATA_STRING = 0x601E,
        TAG_MSI_TRANSFORM_FILE = 0x601F,
        TAG_16BIT_MODULE_NAME = 0x6020,
        TAG_LAYER_DISPLAYNAME = 0x6021,
        TAG_COMPILER_VERSION = 0x6022,
        TAG_ACTION_TYPE = 0x6023,
        TAG_EXPORT_NAME = 0x6024,
        TAG_URL_OR_TAG_VENDOR_ID = 0x6025,
        TAG_DEVICE_ID = 0x6026,
        TAG_SUB_VENDOR_ID = 0x6027,
        TAG_SUB_SYSTEM_ID = 0x6028,
        TAG_PACKAGEID_NAME = 0x6029,
        TAG_PACKAGEID_PUBLISHER = 0x602A,
        TAG_PACKAGEID_LANGUAGE = 0x602B,
        TAG_URL = 0x602C,
        TAG_MANUFACTURER = 0x602D,
        TAG_MODEL = 0x602E,
        TAG_DATE = 0x602F,
        TAG_REG_VALUE_NAME = 0x6030,
        TAG_REG_VALUE_DATA_SZ = 0x6031,
        TAG_MIGRATION_DATA_TEXT = 0x6032,
        TAG_APP_STORE_PRODUCT_ID = 0x6033,
        TAG_MORE_INFO_URL = 0x6034,

        TAG_DATABASE = 0x7001,
        TAG_LIBRARY = 0x7002,
        TAG_INEXCLUDE = 0x7003,
        TAG_SHIM = 0x7004,
        TAG_PATCH = 0x7005,
        TAG_APP = 0x7006,
        TAG_EXE = 0x7007,
        TAG_MATCHING_FILE = 0x7008,
        TAG_SHIM_REF = 0x7009,
        TAG_PATCH_REF = 0x700A,
        TAG_LAYER = 0x700B,
        TAG_FILE = 0x700C,
        TAG_APPHELP = 0x700D,
        TAG_LINK = 0x700E,
        TAG_DATA = 0x700F,
        TAG_MSI_TRANSFORM = 0x7010,
        TAG_MSI_TRANSFORM_REF = 0x7011,
        TAG_MSI_PACKAGE = 0x7012,
        TAG_FLAG = 0x7013,
        TAG_MSI_CUSTOM_ACTION = 0x7014,
        TAG_FLAG_REF = 0x7015,
        TAG_ACTION = 0x7016,
        TAG_LOOKUP = 0x7017,
        TAG_CONTEXT = 0x7018,
        TAG_CONTEXT_REF = 0x7019,
        TAG_KDEVICE = 0x701A,
        TAG_KDRIVER = 0x701C,
        TAG_DEVICE_OR_TAG_MATCHING_DEVICE = 0x701E,
        TAG_ACPI = 0x701F,
        TAG_SPC_OR_TAG_BIOS = 0x7020,
        TAG_CPU = 0x7021,
        TAG_OEM = 0x7022,
        TAG_KFLAG = 0x7023,
        TAG_KFLAG_REF = 0x7024,
        TAG_KSHIM = 0x7025,
        TAG_KSHIM_REF = 0x7026,
        TAG_REINSTALL_UPGRADE = 0x7027,
        TAG_KDATA = 0x7028,
        TAG_BLOCK_UPGRADE = 0x7029,
        TAG_SPC = 0x702A,
        TAG_QUIRK = 0x702B,
        TAG_QUIRK_REF = 0x702C,
        TAG_BIOS_BLOCK = 0x702D,
        TAG_MATCHING_INFO_BLOCK = 0x702E,
        TAG_DEVICE_BLOCK = 0x702F,
        TAG_MIGRATION_DATA = 0x7030,
        TAG_MIGRATION_DATA_REF = 0x7031,
        TAG_MATCHING_REG = 0x7032,
        TAG_MATCHING_TEXT = 0x7033,
        TAG_MACHINE_BLOCK = 0x7034,
        TAG_OS_UPGRADE = 0x7035,
        TAG_PACKAGE = 0x7036,
        TAG_PICK_ONE = 0x7037,
        TAG_MATCH_PLUGIN = 0x7038,
        TAG_MIGRATION_SHIM = 0x7039,
        TAG_UPGRADE_DRIVER_BLOCK = 0x703A,
        TAG_MIGRATION_SHIM_REF = 0x703C,
        TAG_CONTAINS_FILE = 0x703D,
        TAG_CONTAINS_HWID = 0x703E,
        TAG_DRIVER_PACKAGE_BLOCK = 0x703F,

        TAG_STRINGTABLE = 0x7801,
        TAG_INDEXES = 0x7802,
        TAG_INDEX = 0x7803,

        TAG_STRINGTABLE_ITEM = 0x8801,

        TAG_PATCH_BITS = 0x9002,
        TAG_FILE_BITS = 0x9003,
        TAG_EXE_ID = 0x9004,
        TAG_DATA_BITS = 0x9005,
        TAG_MSI_PACKAGE_ID = 0x9006,
        TAG_DATABASE_ID = 0x9007,
        TAG_CONTEXT_PLATFORM_ID = 0x9008,
        TAG_CONTEXT_BRANCH_ID = 0x9009,
        TAG_FIX_ID = 0x9010,
        TAG_APP_ID = 0x9011,
        TAG_REG_VALUE_DATA_BINARY = 0x9012,
        TAG_TEXT = 0x9013,

        TAG_INDEX_BITS = 0x9801
    }

    public SdbFile(byte[] rawBytes, string sourceFile)
    {
        RawBytes = rawBytes;
        SourceFile = Path.GetFullPath(sourceFile);

        MajorVersion = BitConverter.ToInt32(rawBytes, 0);
        MinorVersion = BitConverter.ToInt32(rawBytes, 4);

        Log.Debug("Major: {MajorVersion}, Minor: {MinorVersion}",MajorVersion,MinorVersion);

        var index = 0xc;

        StringTableEntries.Clear();
        Metrics.Clear();

        Children = new List<ISdbEntry>();

        while (index < rawBytes.Length)
        {
            var id = (TagValue) BitConverter.ToUInt16(rawBytes, index);
            index += 2;

            var tagType = (int) id & 0xF000;

            if (tagType == 0x7000)
            {
                //lists look like:
                //tag
                //length
                //data ('length' bytes long)

                var size = BitConverter.ToInt32(rawBytes, index);
                index += 4;
                var buff = new byte[size];
                Buffer.BlockCopy(rawBytes, index, buff, 0, size);

                var c = new Chunk(id, buff, index - 2 - 4);

                var l = new SdbEntryList(id, buff, index - 2 - 4);

                foreach (var sdbEntry in c.Children)
                {
                    if (!(sdbEntry is SdbEntryList))
                    {
                        sdbEntry.Offset += 4;
                    }

                    l.Children.Add(sdbEntry);
                }

                Children.Add(l);

                index += size;
            }
            else
            {
                throw new Exception(
                    $"Unexpected Tag type '0x{tagType:X4}'! Please send this sdb file to saericzimmerman@gmail.com so suppoprt can be added!");
            }
        }
    }
    //https://github.com/evil-e/sdb-explorer/blob/master/sdb.h
    //http://www.geoffchappell.com/studies/windows/win32/apphelp/sdb/tag.htm?tx=46

    public int MajorVersion { get; }
    public int MinorVersion { get; }

    public List<ISdbEntry> Children { get; }

    public static Dictionary<int, StringTableEntry> StringTableEntries { get; } = new Dictionary<int, StringTableEntry>();

    public static Dictionary<TagValue, int> Metrics { get; } = new Dictionary<TagValue, int>();

    public string SourceFile { get; }

    public byte[] RawBytes { get; }
}