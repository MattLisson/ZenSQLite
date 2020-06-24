using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace SQLite_zen.UnitTests {
    class Parent {

        public Guid ID { get; set; }

        [ManyToMany(
            typeof(Child),
            typeof(ParentChild),
            nameof(ParentChild.Parent),
            nameof(ParentChild.Child),
            nameof(ParentChild.Index))]
        public List<Guid> Children { get; set; }
    }

    class Child {

        public Guid ID { get; set; }
    }

    class ParentChild {
        [ForeignKey(typeof(Parent))]
        public Guid Parent { get; set; }

        [ForeignKey(typeof(Child))]
        public Guid Child { get; set; }

        public int Index { get; set; }
    }
}
