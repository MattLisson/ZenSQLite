using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using Xunit;

namespace SQLite_zen.UnitTests {
    public class RelationTypesTests
    {
        private const string DbFilename = "testdb.db3";
        public RelationTypesTests() {
            if (File.Exists(DbFilename)) {
                File.Delete(DbFilename);
            }
        }

        [Fact]
        public void ManyToManyPreservesOrdering()
        {
            Parent parent = new Parent() {
                ID = Guid.NewGuid(),
                Children = new List<Guid> {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                },
            };
            List<Child> children = new List<Child>();
            foreach (Guid childId in parent.Children) {
                children.Add(new Child { ID = childId });
            }
            SQLiteConfig config = new SQLiteConfig(CreateFlags.AllImplicit, DbFilename);
            config
                .AddTable(typeof(Parent))
                .AddTable(typeof(Child))
                .AddTable(typeof(ParentChild))
                .WireForeignKeys();

            using (SQLiteConnection connection = new SQLiteConnection(config)) {
                connection.InsertAll(children);
                connection.Insert(parent);
                parent.Children.Reverse();
                connection.Update(parent);


            }

            using (SQLiteConnection connection = new SQLiteConnection(config)) {
                Parent actualParent = connection.Table<Parent>().First();
                Assert.Equal(3, actualParent.Children.Count);
                for(int i = 0; i < actualParent.Children.Count; i++) {
                    Assert.Equal(parent.Children[i], actualParent.Children[i]);
                }
            }

        }
    }
}
