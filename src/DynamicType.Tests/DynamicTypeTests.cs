using FluentAssertions;
using Xunit;

namespace DynamicType.Tests
{
    public class DynamicTypeTests
    {
        [Fact]
        public void ShouldAddAndGetMember()
        {
            var dynamicObject = new DynamicTypeBuilder().Add("Id", 42).Build();
            dynamicObject.Get<int>("Id").Should().Be(42);
        }

        [Fact]
        public void ShouldBeSameTypeWhenMembersAreEqual()
        {
            var dynamicObject1 = new DynamicTypeBuilder().Add("Id", 42).Build();
            var dynamicObject2 = new DynamicTypeBuilder().Add("Id", 42).Build();
            dynamicObject1.GetType().Should().BeSameAs(dynamicObject2.GetType());
        }

        [Fact]
        public void ShouldNotBeSameTypeWhenTypesAreDifferent()
        {
            var dynamicObject1 = new DynamicTypeBuilder().Add("Id", 42).Build();
            var dynamicObject2 = new DynamicTypeBuilder().Add("Id", (double)42).Build();
            dynamicObject1.GetType().Should().NotBeSameAs(dynamicObject2.GetType());
        }

         [Fact]
        public void ShouldNotBeSameTypeWhenMemberNamesAreDifferent()
        {
            var dynamicObject1 = new DynamicTypeBuilder().Add("Id1", 42).Build();
            var dynamicObject2 = new DynamicTypeBuilder().Add("Id2", 42).Build();
            dynamicObject1.GetType().Should().NotBeSameAs(dynamicObject2.GetType());
        }

        [Fact]
        public void ShouldCopyValuesFromObjectWithProperties()
        {
            var dynamicObject = new DynamicTypeBuilder().From(new ObjectWithPropertiesAndFields(){Id = 42, Name = "SomeName"}).Build();
            dynamicObject.Get<int>("Id").Should().Be(42);
            dynamicObject.Get<string>("Name").Should().Be("SomeName");
        }

        [Fact]
        public void ShouldBeEqualWhenMemberAndValueMatches()
        {
            var dynamicObject1 = new DynamicTypeBuilder().Add("Id1", 42).Build();
            var dynamicObject2 = new DynamicTypeBuilder().Add("Id1", 42).Build();
            dynamicObject1.Should().Be(dynamicObject2);
        }
    }

    public class ObjectWithPropertiesAndFields
    {
        public int Id { get; set; }

        public string Name;
    }
}