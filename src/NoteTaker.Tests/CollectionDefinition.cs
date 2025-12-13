using Xunit;

namespace NoteTaker.Tests;

// This collection definition ensures all tests run sequentially
// to avoid Docker resource conflicts during Aspire testing
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollectionDefinition
{
}