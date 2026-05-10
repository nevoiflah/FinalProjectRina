using MongoDB.Driver;

namespace FinalProjectRina.Server.BL;

public class KnowledgeCache
{
    private readonly IMongoCollection<KnowledgeFact> _collection;
    private List<KnowledgeFact>? _facts;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public KnowledgeCache(IMongoDatabase database)
    {
        _collection = database.GetCollection<KnowledgeFact>("ruppinKnowledge");
    }

    public async Task<List<KnowledgeFact>> GetFactsAsync()
    {
        if (_facts != null) return _facts;

        await _lock.WaitAsync();
        try
        {
            _facts ??= await _collection.Find(Builders<KnowledgeFact>.Filter.Empty).ToListAsync();
            return _facts;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _facts = null;
}
