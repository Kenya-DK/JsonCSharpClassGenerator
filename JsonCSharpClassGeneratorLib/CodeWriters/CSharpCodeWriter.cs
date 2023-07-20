using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace Xamasoft.JsonClassGenerator.CodeWriters
{
    public class CSharpCodeWriter : ICodeWriter
    {
        public string FileExtension
        {
            get { return ".cs"; }
        }

        public string DisplayName
        {
            get { return "C#"; }
        }


        private const string NoRenameAttribute = "[Obfuscation(Feature = \"renaming\", Exclude = true)]";
        private const string NoPruneAttribute = "[Obfuscation(Feature = \"trigger\", Exclude = false)]";

        public string GetTypeName(JsonType type, IJsonClassGeneratorConfig config)
        {
            var arraysAsLists = !config.ExplicitDeserialization;

            switch (type.Type)
            {
                case JsonTypeEnum.Anything: return "object";
                case JsonTypeEnum.Array: return arraysAsLists ? "IList<" + GetTypeName(type.InternalType, config) + ">" : GetTypeName(type.InternalType, config) + "[]";
                case JsonTypeEnum.Dictionary: return "Dictionary<string, " + GetTypeName(type.InternalType, config) + ">";
                case JsonTypeEnum.Boolean: return "bool";
                case JsonTypeEnum.Float: return "double";
                case JsonTypeEnum.Integer: return "int";
                case JsonTypeEnum.Long: return "long";
                case JsonTypeEnum.Date: return "DateTime";
                case JsonTypeEnum.NonConstrained: return "object";
                case JsonTypeEnum.NullableBoolean: return "bool?";
                case JsonTypeEnum.NullableFloat: return "double?";
                case JsonTypeEnum.NullableInteger: return "int?";
                case JsonTypeEnum.NullableLong: return "long?";
                case JsonTypeEnum.NullableDate: return "DateTime?";
                case JsonTypeEnum.NullableSomething: return "object";
                case JsonTypeEnum.Object: return type.AssignedName;
                case JsonTypeEnum.String: return "string";
                default: throw new System.NotSupportedException("Unsupported json type");
            }
        }


        private bool ShouldApplyNoRenamingAttribute(IJsonClassGeneratorConfig config)
        {
            return config.ApplyObfuscationAttributes && !config.ExplicitDeserialization && !config.UsePascalCase;
        }
        private bool ShouldApplyNoPruneAttribute(IJsonClassGeneratorConfig config)
        {
            return config.ApplyObfuscationAttributes && !config.ExplicitDeserialization && config.PropertieMode == PropertyModeEnum.Properties;
        }

        public void WriteFileStart(IJsonClassGeneratorConfig config, TextWriter sw)
        {
            if (config.UseNamespaces)
            {
                foreach (var line in JsonClassGenerator.FileHeader)
                {
                    sw.WriteLine("// " + line);
                }
                sw.WriteLine();
                sw.WriteLine("using System;");
                sw.WriteLine("using System.Collections.Generic;");
                if (ShouldApplyNoPruneAttribute(config) || ShouldApplyNoRenamingAttribute(config))
                    sw.WriteLine("using System.Reflection;");
                if (!config.ExplicitDeserialization && config.UsePascalCase)
                    sw.WriteLine("using Newtonsoft.Json;");
                sw.WriteLine("using Newtonsoft.Json.Linq;");
                if (config.ExplicitDeserialization)
                    sw.WriteLine("using JsonCSharpClassGenerator;");
                if (config.SecondaryNamespace != null && config.HasSecondaryClasses && !config.UseNestedClasses)
                {
                    sw.WriteLine("using {0};", config.SecondaryNamespace);
                }
            }

            if (config.UseNestedClasses)
            {
                sw.WriteLine("    {0} class {1}", config.InternalVisibility ? "internal" : "public", config.MainClass);
                sw.WriteLine("    {");
            }
        }

        public void WriteFileEnd(IJsonClassGeneratorConfig config, TextWriter sw)
        {
            if (config.UseNestedClasses)
            {
                sw.WriteLine("    }");
            }
        }


        public void WriteNamespaceStart(IJsonClassGeneratorConfig config, TextWriter sw, bool root)
        {
            sw.WriteLine();
            sw.WriteLine("namespace {0}", root && !config.UseNestedClasses ? config.Namespace : (config.SecondaryNamespace ?? config.Namespace));
            sw.WriteLine("{");
            sw.WriteLine();
        }

        public void WriteNamespaceEnd(IJsonClassGeneratorConfig config, TextWriter sw, bool root)
        {
            sw.WriteLine("}");
        }

        public void WriteClass(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type)
        {
            var visibility = config.InternalVisibility ? "internal" : "public";

            if (config.ExamplesInDocumentation)
            {
                sw.WriteLine("     /// <summary>");
                sw.WriteLine("     /// Examples: {0}", type.AssignedName);
                sw.WriteLine("     /// </summary>");
            }

            if (config.UseNestedClasses)
            {
                if (!type.IsRoot)
                {
                    if (ShouldApplyNoRenamingAttribute(config)) sw.WriteLine("        " + NoRenameAttribute);
                    if (ShouldApplyNoPruneAttribute(config)) sw.WriteLine("        " + NoPruneAttribute);
                    sw.WriteLine("        {0} class {1}", visibility, type.AssignedName);
                    sw.WriteLine("        {");
                }
            }
            else
            {
                if (ShouldApplyNoRenamingAttribute(config)) sw.WriteLine("    " + NoRenameAttribute);
                if (ShouldApplyNoPruneAttribute(config)) sw.WriteLine("    " + NoPruneAttribute);
                sw.WriteLine("    {0} class {1}", visibility, type.AssignedName);
                sw.WriteLine("    {");
            }

            var prefix = config.UseNestedClasses && !type.IsRoot ? "            " : "        ";


            var shouldSuppressWarning = config.InternalVisibility && !(config.PropertieMode != PropertyModeEnum.Properties) && !config.ExplicitDeserialization;
            if (shouldSuppressWarning)
            {
                sw.WriteLine("#pragma warning disable 0649");
                if (!config.UsePascalCase) sw.WriteLine();
            }

            if (type.IsRoot && config.ExplicitDeserialization) WriteStringConstructorExplicitDeserialization(config, sw, type, prefix);

            if (config.ExplicitDeserialization)
            {
                if (config.PropertieMode == PropertyModeEnum.Properties) WriteClassWithPropertiesExplicitDeserialization(sw, type, prefix);
                else WriteClassWithFieldsExplicitDeserialization(sw, type, prefix);
            }
            else
            {
                WriteClassMembers(config, sw, type, prefix);
            }

            if (shouldSuppressWarning)
            {
                sw.WriteLine();
                sw.WriteLine("#pragma warning restore 0649");
                sw.WriteLine();
            }


            if (config.UseNestedClasses && !type.IsRoot)
                sw.WriteLine("        }");

            if (!config.UseNestedClasses)
                sw.WriteLine("    }");

            sw.WriteLine();


        }

        private void WriteClassMembers(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type, string prefix)
        {
            IList<FieldInfo> theFields = type.Fields;
            if (config.SortMemberFields) theFields = theFields.OrderBy(f => f.JsonMemberName).ToList();


            if (config.UseRegions)
            {
                sw.WriteLine(prefix + "#region Const/Static Values");
                sw.WriteLine(prefix + "#endregion");
            }

            if (config.UseRegions)
                sw.WriteLine(prefix + "#region Private Values");

            if (config.PropertieMode == PropertyModeEnum.FullProperty)
                foreach (var field in theFields)
                    sw.WriteLine(prefix + "private {0} {1};", field.Type.GetTypeName(), "_" + field.JsonMemberName);

            if (config.UseRegions)
                sw.WriteLine(prefix + "#endregion");


            if (config.UseRegions)
                sw.WriteLine(prefix + "#region New");

            if (config.CreateNew)
            {
                if (config.ExamplesInDocumentation)
                {
                    sw.WriteLine(prefix + "/// <summary>");
                    sw.WriteLine(prefix + "/// Creates a new generic <see cref=\"{0}\"/>", type.AssignedName);
                    sw.WriteLine(prefix + "/// </summary>");
                }
                sw.WriteLine(prefix + "public {0}()", type.AssignedName);
                sw.WriteLine(prefix + "{");
                var listTypeFilds = theFields.Where(f => f.Type.GetTypeName().ToLower().Contains("list")).ToList();
                foreach (var field in listTypeFilds)
                {
                    switch (config.PropertieMode)
                    {
                        case PropertyModeEnum.Properties:
                        case PropertyModeEnum.Fields:
                            sw.WriteLine(prefix + "    {0} = new {1}();", field.MemberName, field.Type.GetTypeName());
                            break;
                        case PropertyModeEnum.FullProperty:
                            sw.WriteLine(prefix + "    {0} = new {1}();", ("_" + field.JsonMemberName), field.Type.GetTypeName());
                            break;
                    }
                }
                //sw.WriteLine(prefix + prefix + "set { _" + field.JsonMemberName + " = value; }");
                sw.WriteLine(prefix + "}");
            }


            if (config.UseRegions)
                sw.WriteLine(prefix + "#endregion");


            if (config.UseRegions)
                sw.WriteLine(prefix + "#region Methods");



            if (config.UseRegions)
                sw.WriteLine(prefix + "#endregion");

            if (config.UseRegions)
                sw.WriteLine(prefix + "#region Override Methods");



            if (config.UseRegions)
                sw.WriteLine(prefix + "#endregion");

            if (config.UseRegions)
                sw.WriteLine(prefix + "#region Method Get Or Set");

            foreach (var field in theFields)
            {
                if (config.UsePascalCase || config.ExamplesInDocumentation)
                    sw.WriteLine();

                if (config.ExamplesInDocumentation)
                {
                    sw.WriteLine(prefix + "/// <summary>");
                    sw.WriteLine(prefix + "/// Examples: " + field.GetExamplesText());
                    sw.WriteLine(prefix + "/// </summary>");
                }

                if (config.UsePascalCase)
                    sw.WriteLine(prefix + "[JsonProperty(\"{0}\")]", field.JsonMemberName);

                switch (config.PropertieMode)
                {
                    case PropertyModeEnum.Properties:
                        sw.WriteLine(prefix + "public {0} {1} {{ get; set; }}", field.Type.GetTypeName(), field.MemberName);
                        break;
                    case PropertyModeEnum.Fields:
                        sw.WriteLine(prefix + "public {0} {1};", field.Type.GetTypeName(), field.MemberName);
                        break;
                    case PropertyModeEnum.FullProperty:
                        sw.WriteLine(prefix + "public {0} {1}", field.Type.GetTypeName(), field.MemberName);
                        sw.WriteLine(prefix + "{");
                        sw.WriteLine(prefix + "    get => {0};", "_" + field.JsonMemberName);
                        sw.WriteLine(prefix + "    set { _" + field.JsonMemberName + " = value; }");
                        sw.WriteLine(prefix + "}");
                        break;
                }
            }
            if (config.UseRegions)
                sw.WriteLine(prefix + "#endregion");

            if (config.UseRegions)
                sw.WriteLine(prefix + "#region ICopyable<{0}> Members", type.AssignedName);

            if (config.CreateCopyable)
            {
                // Create ShallowCopy
                if (config.ExamplesInDocumentation)
                {
                    sw.WriteLine(prefix + "/// <summary>");
                    sw.WriteLine(prefix + "/// Creates a shallow copy of this <see cref=\"{0}\"/> instance.", type.AssignedName);
                    sw.WriteLine(prefix + "/// </summary>");
                    sw.WriteLine(prefix + "/// <returns>The shallow copy.</returns>  ");
                }
                sw.WriteLine(prefix + "public T ShallowCopy<T>() where T : {0}", type.AssignedName);
                sw.WriteLine(prefix + "{");
                sw.WriteLine(prefix + "    return (T)MemberwiseClone();");
                sw.WriteLine(prefix + "}");

                // Create DeepCopy
                if (config.ExamplesInDocumentation)
                {
                    sw.WriteLine(prefix + "/// <summary>");
                    sw.WriteLine(prefix + "/// Creates a deep copy of this <see cref=\"{0}\"/> instance.", type.AssignedName);
                    sw.WriteLine(prefix + "/// </summary>");
                    sw.WriteLine(prefix + "/// <returns>The deep copy.</returns>  ");
                }
                sw.WriteLine(prefix + "public T DeepCopy<T>() where T : {0}", type.AssignedName);
                sw.WriteLine(prefix + "{");
                sw.WriteLine(prefix + "    var clone = (T)MemberwiseClone();");
                sw.WriteLine(prefix + "    return clone;");
                sw.WriteLine(prefix + "}");
            }

            if (config.UseRegions)
                sw.WriteLine(prefix + "#endregion");
        }


        #region Code for (obsolete) explicit deserialization
        private void WriteClassWithPropertiesExplicitDeserialization(TextWriter sw, JsonType type, string prefix)
        {

            sw.WriteLine(prefix + "private JObject __jobject;");
            sw.WriteLine(prefix + "public {0}(JObject obj)", type.AssignedName);
            sw.WriteLine(prefix + "{");
            sw.WriteLine(prefix + "    this.__jobject = obj;");
            sw.WriteLine(prefix + "}");
            sw.WriteLine();

            foreach (var field in type.Fields)
            {

                string variable = null;
                if (field.Type.MustCache)
                {
                    variable = "_" + char.ToLower(field.MemberName[0]) + field.MemberName.Substring(1);
                    sw.WriteLine(prefix + "[System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]");
                    sw.WriteLine(prefix + "private {0} {1};", field.Type.GetTypeName(), variable);
                }


                sw.WriteLine(prefix + "public {0} {1}", field.Type.GetTypeName(), field.MemberName);
                sw.WriteLine(prefix + "{");
                sw.WriteLine(prefix + "    get");
                sw.WriteLine(prefix + "    {");
                if (field.Type.MustCache)
                {
                    sw.WriteLine(prefix + "        if ({0} == null)", variable);
                    sw.WriteLine(prefix + "            {0} = {1};", variable, field.GetGenerationCode("__jobject"));
                    sw.WriteLine(prefix + "        return {0};", variable);
                }
                else
                {
                    sw.WriteLine(prefix + "        return {0};", field.GetGenerationCode("__jobject"));
                }
                sw.WriteLine(prefix + "    }");
                sw.WriteLine(prefix + "}");
                sw.WriteLine();

            }

        }


        private void WriteStringConstructorExplicitDeserialization(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type, string prefix)
        {
            sw.WriteLine();
            sw.WriteLine(prefix + "public {1}(string json)", config.InternalVisibility ? "internal" : "public", type.AssignedName);
            sw.WriteLine(prefix + "    : this(JObject.Parse(json))");
            sw.WriteLine(prefix + "{");
            sw.WriteLine(prefix + "}");
            sw.WriteLine();
        }

        private void WriteClassWithFieldsExplicitDeserialization(TextWriter sw, JsonType type, string prefix)
        {


            sw.WriteLine(prefix + "public {0}(JObject obj)", type.AssignedName);
            sw.WriteLine(prefix + "{");

            foreach (var field in type.Fields)
            {
                sw.WriteLine(prefix + "    this.{0} = {1};", field.MemberName, field.GetGenerationCode("obj"));

            }

            sw.WriteLine(prefix + "}");
            sw.WriteLine();

            foreach (var field in type.Fields)
            {
                sw.WriteLine(prefix + "public readonly {0} {1};", field.Type.GetTypeName(), field.MemberName);
            }
        }
        #endregion

    }
}
