#pragma checksum "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/InlineBlocks.cshtml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "e7fa74a13b1e78fed87942ccd83aa733810e8664"
// <auto-generated/>
#pragma warning disable 1591
[assembly: global::Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemAttribute(typeof(Microsoft.AspNetCore.Razor.Language.IntegrationTests.TestFiles.TestFiles_IntegrationTests_CodeGenerationIntegrationTest_InlineBlocks_Runtime), @"default", @"/TestFiles/IntegrationTests/CodeGenerationIntegrationTest/InlineBlocks.cshtml")]
namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests.TestFiles
{
    #line hidden
    [global::Microsoft.AspNetCore.Razor.Hosting.RazorSourceChecksumAttribute(@"SHA1", @"e7fa74a13b1e78fed87942ccd83aa733810e8664", @"/TestFiles/IntegrationTests/CodeGenerationIntegrationTest/InlineBlocks.cshtml")]
    public class TestFiles_IntegrationTests_CodeGenerationIntegrationTest_InlineBlocks_Runtime
    {
        #pragma warning disable 1998
        public async System.Threading.Tasks.Task ExecuteAsync()
        {
            WriteLiteral("(string link) {\r\n    <a");
            BeginWriteAttribute("href", " href=\"", 36, "\"", 94, 1);
            WriteAttributeValue("", 43, new Microsoft.AspNetCore.Mvc.Razor.HelperResult(async(__razor_attribute_value_writer) => {
                PushWriter(__razor_attribute_value_writer);
#nullable restore
#line 2 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/InlineBlocks.cshtml"
              if(link != null) { 

#line default
#line hidden
#nullable disable
#nullable restore
#line (2,35)-(2,39) 6 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/InlineBlocks.cshtml"
Write(link);

#line default
#line hidden
#nullable disable
#nullable restore
#line 2 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/InlineBlocks.cshtml"
                                       } else { 

#line default
#line hidden
#nullable disable
                WriteLiteral("#");
#nullable restore
#line 2 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/InlineBlocks.cshtml"
                                                               }

#line default
#line hidden
#nullable disable
                PopWriter();
            }
            ), 43, 51, false);
            EndWriteAttribute();
            WriteLiteral(" />\r\n}");
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591