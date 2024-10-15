using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamasoft.JsonClassGenerator.CodeWriters
{
    public class RustCodeWriter : ICodeWriter
    {
        public string FileExtension
        {
            get { return ".rs"; }
        }

        public string DisplayName
        {
            get { return "Rust"; }
        }

        public string GetTypeName(JsonType type, IJsonClassGeneratorConfig config)
        {
            switch (type.Type)
            {
                case JsonTypeEnum.Anything: return "any";
                case JsonTypeEnum.String: return "String";
                case JsonTypeEnum.Boolean: return "bool";
                case JsonTypeEnum.Integer: return "i32";
                case JsonTypeEnum.Long: return "i64";
                case JsonTypeEnum.Float: return "f64";
                case JsonTypeEnum.Date: return "String";
                case JsonTypeEnum.NullableInteger: return "Option<i32>";
                case JsonTypeEnum.NullableLong: return "Option<i64>";
                case JsonTypeEnum.NullableFloat: return "Option<f64>";
                case JsonTypeEnum.NullableBoolean: return "Option<bool>";
                case JsonTypeEnum.NullableDate: return "Option<String>";
                case JsonTypeEnum.Object: return type.AssignedName;
                case JsonTypeEnum.Array: return "Vec<" + GetTypeName(type.InternalType, config) + ">";
                //case JsonTypeEnum.Dictionary: return "{ [key: string]: " + GetTypeName(type.InternalType, config) + "; }";
                case JsonTypeEnum.NullableSomething: return "Option<String>";
                case JsonTypeEnum.NonConstrained: return "Option<String>";
                default: throw new NotSupportedException("Unsupported type");
            }
        }
        public bool IsNullable(JsonType type)
        {
            switch (type.Type)
            {
                case JsonTypeEnum.NullableInteger:
                case JsonTypeEnum.NullableLong:
                case JsonTypeEnum.NullableFloat:
                case JsonTypeEnum.NullableBoolean:
                case JsonTypeEnum.NullableDate:
                case JsonTypeEnum.NullableSomething:
                case JsonTypeEnum.NonConstrained:
                    return true;
                default: return false;
            }
        }
        public void WriteClass(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type)
        {
            var prefix = "";
            var exported = !config.InternalVisibility || config.SecondaryNamespace != null;
            sw.WriteLine(prefix + "#[derive(Deserialize, Serialize, Clone, Debug)]");
            sw.WriteLine(prefix + (exported ? "pub " : string.Empty) + "struct " + type.AssignedName + " {");
            foreach (var field in type.Fields)
            {
                var shouldDefineNamespace = type.IsRoot && config.SecondaryNamespace != null && config.Namespace != null && (field.Type.Type == JsonTypeEnum.Object || (field.Type.InternalType != null && field.Type.InternalType.Type == JsonTypeEnum.Object));
                if (config.ExamplesInDocumentation)
                {
                    sw.WriteLine();
                    sw.WriteLine(prefix + "    // Examples: " + field.GetExamplesText());
                }
                if (IsNullable(field.Type))
                    sw.WriteLine(prefix + "    " + "#[serde(skip_serializing_if = \"Option::is_none\")]");
                sw.WriteLine(prefix + "    " + "#[serde(rename = \"" + field.JsonMemberName + "\")]");
                sw.WriteLine(prefix + "    " + (exported ? "pub " : string.Empty) + field.SnakeCase + ": " + (shouldDefineNamespace ? config.SecondaryNamespace + "." : string.Empty) + GetTypeName(field.Type, config) + ",");
            }
            sw.WriteLine(prefix + "}");
            sw.WriteLine();
        }

        private bool IsNullable(JsonTypeEnum type)
        {
            return
                type == JsonTypeEnum.NullableBoolean ||
                type == JsonTypeEnum.NullableDate ||
                type == JsonTypeEnum.NullableFloat ||
                type == JsonTypeEnum.NullableInteger ||
                type == JsonTypeEnum.NullableLong ||
                type == JsonTypeEnum.NullableSomething;
        }

        public void WriteFileStart(IJsonClassGeneratorConfig config, TextWriter sw)
        {
            foreach (var line in JsonClassGenerator.FileHeader)
            {
                sw.WriteLine("// " + line);
            }
            sw.WriteLine("use serde::{Deserialize, Serialize};");
            sw.WriteLine();
        }

        public void WriteFileEnd(IJsonClassGeneratorConfig config, TextWriter sw)
        {
        }

        private string GetNamespace(IJsonClassGeneratorConfig config, bool root)
        {
            return root ? config.Namespace : (config.SecondaryNamespace ?? config.Namespace);
        }

        public void WriteNamespaceStart(IJsonClassGeneratorConfig config, TextWriter sw, bool root)
        {
        }

        public void WriteNamespaceEnd(IJsonClassGeneratorConfig config, TextWriter sw, bool root)
        {
        }

    }
}
