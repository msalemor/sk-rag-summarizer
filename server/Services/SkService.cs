using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Skills.Core;
using server.Models;

namespace server.Services;

public class SkService
{
    public string DocPath { get; set; } = null!;
    public async Task<string> SkMemoryGetAsync(string collection, string key, TextMemorySkill memorySkill)
    {
        return await memorySkill.RetrieveAsync(collection, key, null);
    }

    public async Task<string> SkSaveMemoryAsync(IKernel kernel, Memory memory, TextMemorySkill memorySkill)
    {
        var skMemory = await memorySkill.RetrieveAsync(memory.collection, memory.key, null);
        if (skMemory is not null)
        {
            await kernel.Memory.RemoveAsync(memory.collection, memory.key);
        }
        var parts = memory.key.Split("-");
        var jsonObj = new { docID = parts[0] };
        var json = JsonSerializer.Serialize(jsonObj);
        return await kernel.Memory.SaveInformationAsync(memory.collection, memory.text, memory.key, parts[0], json);
    }

    public async Task<bool> SkDeleteMemoryAsync(IKernel kernel, Memory memory)
    {
        try
        {
            await kernel.Memory.RemoveAsync(memory.collection, memory.key);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        return false;
    }

    public async Task<Completion> SkQueryAsync(IKernel kernel, Query query)
    {
        IAsyncEnumerable<MemoryQueryResult> queryResults =
            kernel.Memory.SearchAsync(query.collection, query.query, limit: query.limit, minRelevanceScore: query.minRelevanceScore);

        var promptData = new StringBuilder();
        await foreach (MemoryQueryResult r in queryResults)
        {
            promptData.Append(r.Metadata.Text + "\n\n");
        }

        const string ragFunctionDefinition = "{{$input}}\n\nText:\n\"\"\"{{$data}}\n\"\"\"";
        var ragFunction = kernel.CreateSemanticFunction(ragFunctionDefinition, maxTokens: query.maxTokens);
        var result = await kernel.RunAsync(ragFunction, new(query.query)
        {
            ["data"] = promptData.ToString()
        });
        return new Completion(query.query, result.ToString(), result.ModelResults.LastOrDefault()?.GetOpenAIChatResult()?.Usage);
    }
}