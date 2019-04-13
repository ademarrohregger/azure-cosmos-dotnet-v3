﻿//-----------------------------------------------------------------------
// <copyright file="LazyCosmosElementTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Patchable;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for LazyCosmosElementTests.
    /// </summary>
    //[Ignore]
    [TestClass]
    public class PatchableCosmosElementTest
    {
        private class PatchablePerson
        {
            private readonly PatchableCosmosObject patchableCosmosObject;

            public PatchablePerson(PatchableCosmosObject patchableCosmosObject)
            {
                this.patchableCosmosObject = patchableCosmosObject;
            }

            public string Name
            {
                get
                {
                    return (this.patchableCosmosObject[nameof(Person.Name)].ToCosmosElement() as CosmosString).Value;
                }
                set
                {
                    this.patchableCosmosObject.Replace(nameof(Person.Name), CosmosString.Create(value));
                }
            }

            public int Age
            {
                get
                {
                    CosmosNumber cosmosNumber = this.patchableCosmosObject[nameof(Person.Age)].ToCosmosElement() as CosmosNumber;

                    int age;
                    if (cosmosNumber.IsFloatingPoint)
                    {
                        age = (int)cosmosNumber.AsFloatingPoint().Value;
                    }
                    else
                    {
                        age = (int)cosmosNumber.AsInteger().Value;
                    }

                    return age;
                }
                set
                {
                    this.patchableCosmosObject.Replace(nameof(Person.Age), CosmosNumber.Create(value));
                }
            }

            public IEnumerable<PatchablePerson> Children
            {
                get
                {
                    PatchableCosmosArray children = this.patchableCosmosObject[nameof(Person.Children)] as PatchableCosmosArray;
                    for(int i = 0; i < children.Count; i++)
                    {
                        yield return new PatchablePerson((PatchableCosmosObject)children[i]);
                    }
                }
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestQuickNavigation()
        {
            Person person = new Person("Alice", 50, new Person[] { new Person("Bob", 25, new Person[] { }) });
            string personString = JsonConvert.SerializeObject(person);
            CosmosObject cosmosObject = JsonConvert.DeserializeObject<CosmosObject>(personString);
            PatchableCosmosObject patchableCosmosObject = (PatchableCosmosObject)cosmosObject.ToPatchable();
            PatchablePerson patchablePerson = new PatchablePerson(patchableCosmosObject);

            Assert.AreEqual(patchablePerson.Name, person.Name);
            Assert.AreEqual(patchablePerson.Age, person.Age);
            Assert.AreEqual(patchablePerson.Children.First().Name, person.Children[0].Name);
            Assert.AreEqual(patchablePerson.Children.First().Age, person.Children[0].Age);
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestQuickPatches()
        {
            Person person = new Person("Alice", 50, new Person[] { new Person("Bob", 25, new Person[] { }) });
            string personString = JsonConvert.SerializeObject(person);
            CosmosObject cosmosObject = JsonConvert.DeserializeObject<CosmosObject>(personString);
            PatchableCosmosObject patchableCosmosObject = (PatchableCosmosObject)cosmosObject.ToPatchable();
            PatchablePerson patchablePerson = new PatchablePerson(patchableCosmosObject);

            patchablePerson.Age = 123;
            Assert.AreEqual(123, patchablePerson.Age);

            patchablePerson.Name = "Cosmos";
            Assert.AreEqual("Cosmos", patchablePerson.Name);

            patchablePerson.Children.First().Age = 1337;
            Assert.AreEqual(1337, patchablePerson.Children.First().Age);

            patchablePerson.Children.First().Name = "DB";
            Assert.AreEqual("DB", patchablePerson.Children.First().Name);
        }

        #region PatchableArray
        [TestMethod]
        [Owner("brchon")]
        public void TestArrayAdd()
        {
            PatchableCosmosArray patchableCosmosArray = PatchableCosmosArray.Create(
                CosmosArray.Create(new CosmosElement[] { }));

            // Sequential adds
            int numAdds = 3;
            for(int i = 0; i < numAdds; i++)
            {
                patchableCosmosArray.Add(i, CosmosNumber.Create(i));
            }

            for (int i = 0; i < numAdds; i++)
            {
                Assert.AreEqual(i, ((CosmosNumber)patchableCosmosArray[i].ToCosmosElement()).AsInteger().Value);
            }

            // Insert in the middle
            patchableCosmosArray.Add(numAdds / 2 , CosmosNumber.Create(42));

            for (int i = 0; i < numAdds / 2; i++)
            {
                Assert.AreEqual(i, ((CosmosNumber)patchableCosmosArray[i].ToCosmosElement()).AsInteger().Value);
            }

            Assert.AreEqual(42, ((CosmosNumber)patchableCosmosArray[numAdds / 2].ToCosmosElement()).AsInteger().Value);

            for (int i = numAdds / 2 + 1; i < numAdds + 1; i++)
            {
                Assert.AreEqual(i - 1, ((CosmosNumber)patchableCosmosArray[i].ToCosmosElement()).AsInteger().Value);
            }

            try
            {
                // Can not add a null element to the array
                patchableCosmosArray.Add(index: 0, item: null);
                Assert.Fail($"Expected {nameof(PatchableCosmosArray)} to throw an {nameof(ArgumentNullException)} when trying to insert a null element.");
            }
            catch(ArgumentNullException)
            {
            }

            try
            {
                // Can not add past the end of the array
                patchableCosmosArray.Add(index: 13387, item: CosmosNumber.Create(42));
                Assert.Fail($"Expected {nameof(PatchableCosmosArray)} to throw an {nameof(ArgumentOutOfRangeException)} when trying to insert an element past the end of the array.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestArrayIndex()
        {
            // Create an array of array
            PatchableCosmosArray patchableCosmosArray = PatchableCosmosArray.Create(
                CosmosArray.Create(new CosmosElement[] 
                {
                    CosmosArray.Create(new CosmosElement[] { })
                })); 
            
            // [[]]
            // Add an item to the inner array.
            PatchableCosmosArray patchableCosmosSubArray = (PatchableCosmosArray)patchableCosmosArray[0];
            patchableCosmosSubArray.Add(0, CosmosNumber.Create(42));

            // [[42]]
            // Check that the change propogates to the parent context
            CosmosNumber cosmosNumber = (CosmosNumber)((PatchableCosmosNumber)(((PatchableCosmosArray)(patchableCosmosArray[0]))[0])).ToCosmosElement();
            Assert.AreEqual(42, cosmosNumber.AsInteger().Value);

            try
            {
                // Can not index out of bound
                PatchableCosmosArray arrayOutOfBounds = (PatchableCosmosArray)patchableCosmosArray[1337];
                Assert.Fail($"Expected {nameof(PatchableCosmosArray)} to throw an {nameof(ArgumentOutOfRangeException)} when trying to remove an index that does not exist.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestArrayRemove()
        {
            PatchableCosmosArray patchableCosmosArray = PatchableCosmosArray.Create(
                CosmosArray.Create(
                    new CosmosElement[]
                    {
                        CosmosNumber.Create(0),
                        CosmosNumber.Create(1),
                        CosmosNumber.Create(2)
                    }));

            Assert.AreEqual(3, patchableCosmosArray.Count);
            Assert.AreEqual(0, ((CosmosNumber)patchableCosmosArray[0].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(1, ((CosmosNumber)patchableCosmosArray[1].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(2, ((CosmosNumber)patchableCosmosArray[2].ToCosmosElement()).AsInteger().Value);

            // Remove from the middle
            patchableCosmosArray.Remove(1);
            Assert.AreEqual(2, patchableCosmosArray.Count);
            Assert.AreEqual(0, ((CosmosNumber)patchableCosmosArray[0].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(2, ((CosmosNumber)patchableCosmosArray[1].ToCosmosElement()).AsInteger().Value);

            // Remove the rest
            patchableCosmosArray.Remove(0);
            patchableCosmosArray.Remove(0);
            Assert.AreEqual(0, patchableCosmosArray.Count);

            try
            {
                // Can not remove from an index that does not exist
                patchableCosmosArray.Remove(1337);
                Assert.Fail($"Expected {nameof(PatchableCosmosArray)} to throw an {nameof(ArgumentOutOfRangeException)} when trying to remove an index that does not exist.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestArrayReplace()
        {
            PatchableCosmosArray patchableCosmosArray = PatchableCosmosArray.Create(
                CosmosArray.Create(
                    new CosmosElement[]
                    {
                        CosmosNumber.Create(0),
                        CosmosNumber.Create(1),
                        CosmosNumber.Create(2)
                    }));

            Assert.AreEqual(3, patchableCosmosArray.Count);
            Assert.AreEqual(0, ((CosmosNumber)patchableCosmosArray[0].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(1, ((CosmosNumber)patchableCosmosArray[1].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(2, ((CosmosNumber)patchableCosmosArray[2].ToCosmosElement()).AsInteger().Value);

            // Replace from the middle
            patchableCosmosArray.Replace(1, CosmosNumber.Create(42));
            Assert.AreEqual(3, patchableCosmosArray.Count);
            Assert.AreEqual(0, ((CosmosNumber)patchableCosmosArray[0].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(42, ((CosmosNumber)patchableCosmosArray[1].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(2, ((CosmosNumber)patchableCosmosArray[2].ToCosmosElement()).AsInteger().Value);

            try
            {
                // Can not replace with a null element
                patchableCosmosArray.Replace(index: 0, item: null);
                Assert.Fail($"Expected {nameof(PatchableCosmosArray)} to throw an {nameof(ArgumentNullException)} when trying to replace an element with a null element.");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                // Can not replace from an index that does not exist
                patchableCosmosArray.Replace(1337, CosmosNumber.Create(42));
                Assert.Fail($"Expected {nameof(PatchableCosmosArray)} to throw an {nameof(ArgumentOutOfRangeException)} when trying to replace an index that does not exist.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestArrayPatchThenRead()
        {
            // Start with a cosmos array that may or may not be from a byte array.
            CosmosArray cosmosArray = CosmosArray.Create(
                new CosmosElement[]
                {
                    CosmosNumber.Create(0),
                    CosmosNumber.Create(1),
                    CosmosNumber.Create(2),
                });

            // If an update is needed convert it into a patchable cosmos array
            PatchableCosmosArray patchableCosmosArray = (PatchableCosmosArray)cosmosArray.ToPatchable();
            Assert.AreEqual(3, patchableCosmosArray.Count);
            Assert.AreEqual(0, ((CosmosNumber)patchableCosmosArray[0].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(1, ((CosmosNumber)patchableCosmosArray[1].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(2, ((CosmosNumber)patchableCosmosArray[2].ToCosmosElement()).AsInteger().Value);

            // Do a series of patch operations
            patchableCosmosArray.Add(0, CosmosNumber.Create(42)); // 42, 0, 1, 2
            patchableCosmosArray.Remove(1); // 42, 1, 2
            patchableCosmosArray.Replace(2, CosmosNumber.Create(1337)); // 42, 1, 1337
            Assert.AreEqual(3, patchableCosmosArray.Count);
            Assert.AreEqual(42, ((CosmosNumber)patchableCosmosArray[0].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(1, ((CosmosNumber)patchableCosmosArray[1].ToCosmosElement()).AsInteger().Value);
            Assert.AreEqual(1337, ((CosmosNumber)patchableCosmosArray[2].ToCosmosElement()).AsInteger().Value);

            // Convert it back into a CosmosElement for navigation
            CosmosArray patchedCosmosArrayWrapper = (CosmosArray)patchableCosmosArray.ToCosmosElement();
            Assert.AreEqual(3, patchedCosmosArrayWrapper.Count);
            Assert.AreEqual(42, ((CosmosNumber)patchedCosmosArrayWrapper[0]).AsInteger().Value);
            Assert.AreEqual(1, ((CosmosNumber)patchedCosmosArrayWrapper[1]).AsInteger().Value);
            Assert.AreEqual(1337, ((CosmosNumber)patchedCosmosArrayWrapper[2]).AsInteger().Value);

            // And then additional conversions should not change the data
            PatchableCosmosArray patchableCosmosArray2 = (PatchableCosmosArray)patchedCosmosArrayWrapper.ToPatchable();
            CosmosArray patchedCosmosArrayWrapper2 = (CosmosArray)patchableCosmosArray2.ToCosmosElement();
            Assert.AreEqual(3, patchedCosmosArrayWrapper.Count);
            Assert.AreEqual(42, ((CosmosNumber)patchedCosmosArrayWrapper[0]).AsInteger().Value);
            Assert.AreEqual(1, ((CosmosNumber)patchedCosmosArrayWrapper[1]).AsInteger().Value);
            Assert.AreEqual(1337, ((CosmosNumber)patchedCosmosArrayWrapper[2]).AsInteger().Value);
        }
        #endregion
        #region PatchableObject
        [TestMethod]
        [Owner("brchon")]
        public void TestObjectAdd()
        {
            PatchableCosmosObject patchableCosmosObject = PatchableCosmosObject.Create(
                CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "name", CosmosString.Create("Bob") },
                        { "age", CosmosNumber.Create(21) }
                    }));

            Assert.AreEqual("Bob", ((CosmosString)patchableCosmosObject["name"].ToCosmosElement()).Value);
            Assert.AreEqual(21, ((CosmosNumber)patchableCosmosObject["age"].ToCosmosElement()).AsInteger().Value);

            // Adds
            patchableCosmosObject.Add("height", CosmosNumber.Create(72));
            Assert.AreEqual(72, ((CosmosNumber)patchableCosmosObject["height"].ToCosmosElement()).AsInteger().Value);

            try
            {
                // Can not add a null key to the object
                patchableCosmosObject.Add("asdf", value: null);
                Assert.Fail($"Expected {nameof(PatchableCosmosObject)} to throw an {nameof(ArgumentNullException)} when trying to insert a null element.");
            }
            catch (ArgumentNullException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestObjectIndex()
        {
            PatchableCosmosObject patchableCosmosObject = PatchableCosmosObject.Create(
                CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "inner", CosmosObject.Create(new Dictionary<string, CosmosElement>()) },
                    }));

            // {"inner" : {}}
            // Add an item to the inner context
            PatchableCosmosObject patchableCosmosSubObject = (PatchableCosmosObject)patchableCosmosObject["inner"];
            patchableCosmosSubObject.Add("inner2", CosmosNumber.Create(42));

            // {"inner" : {"inner2" : 42}}
            // See that the patch propogate to the parent context.
            CosmosNumber cosmosNumber = (CosmosNumber)((PatchableCosmosNumber)((PatchableCosmosObject)patchableCosmosObject["inner"])["inner2"]).ToCosmosElement();
            Assert.AreEqual(42, cosmosNumber.AsInteger().Value);

            try
            {
                // Can not add a null key to the object
                patchableCosmosObject.Add("asdf", value: null);
                Assert.Fail($"Expected {nameof(PatchableCosmosObject)} to throw an {nameof(ArgumentNullException)} when trying to insert a null element.");
            }
            catch (ArgumentNullException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestObjectRemove()
        {
            PatchableCosmosObject patchableCosmosObject = PatchableCosmosObject.Create(
                CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "name", CosmosString.Create("Bob") },
                        { "age", CosmosNumber.Create(21) }
                    }));

            Assert.AreEqual("Bob", ((CosmosString)patchableCosmosObject["name"].ToCosmosElement()).Value);
            Assert.AreEqual(21, ((CosmosNumber)patchableCosmosObject["age"].ToCosmosElement()).AsInteger().Value);

            // Removes
            patchableCosmosObject.Remove("name");
            patchableCosmosObject.Remove("age");
            Assert.IsFalse(patchableCosmosObject.ContainsKey("name"));
            Assert.IsFalse(patchableCosmosObject.ContainsKey("age"));

            try
            {
                // Can not remove a null key
                patchableCosmosObject.Remove(null);
                Assert.Fail($"Expected {nameof(PatchableCosmosObject)} to throw an {nameof(ArgumentNullException)} when trying to insert a null element.");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                // Can not remove a key that does not exist
                patchableCosmosObject.Remove("key that does not exist");
                Assert.Fail($"Expected {nameof(PatchableCosmosObject)} to throw an {nameof(ArgumentNullException)} when trying to insert a null element.");
            }
            catch (KeyNotFoundException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestObjectReplace()
        {
            PatchableCosmosObject patchableCosmosObject = PatchableCosmosObject.Create(
                CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "name", CosmosString.Create("Bob") },
                        { "age", CosmosNumber.Create(21) }
                    }));

            Assert.AreEqual("Bob", ((CosmosString)patchableCosmosObject["name"].ToCosmosElement()).Value);
            Assert.AreEqual(21, ((CosmosNumber)patchableCosmosObject["age"].ToCosmosElement()).AsInteger().Value);

            // Replace
            patchableCosmosObject.Replace("age", CosmosNumber.Create(42));
            Assert.AreEqual("Bob", ((CosmosString)patchableCosmosObject["name"].ToCosmosElement()).Value);
            Assert.AreEqual(42, ((CosmosNumber)patchableCosmosObject["age"].ToCosmosElement()).AsInteger().Value);

            try
            {
                // Can not replace a null key
                patchableCosmosObject.Replace(key: null, value: CosmosNumber.Create(42));
                Assert.Fail($"Expected {nameof(PatchableCosmosObject)} to throw an {nameof(ArgumentNullException)} when trying to insert a null element.");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                // Can not replace a key that does not exist
                patchableCosmosObject.Replace("key that does not exist", CosmosNumber.Create(42));
                Assert.Fail($"Expected {nameof(PatchableCosmosObject)} to throw an {nameof(ArgumentNullException)} when trying to insert a null element.");
            }
            catch (KeyNotFoundException)
            {
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestObjectPatchThenRead()
        {
            // Start with a cosmos array that may or may not be from a byte array.
            CosmosObject cosmosObject = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { "name", CosmosString.Create("Bob") },
                    { "age", CosmosNumber.Create(21) }
                });

            // If an update is needed convert it into a patchable cosmos object
            PatchableCosmosObject patchableCosmosObject = (PatchableCosmosObject)cosmosObject.ToPatchable();
            Assert.AreEqual("Bob", ((CosmosString)patchableCosmosObject["name"].ToCosmosElement()).Value);
            Assert.AreEqual(21, ((CosmosNumber)patchableCosmosObject["age"].ToCosmosElement()).AsInteger().Value);

            // Do a series of patch operations
            patchableCosmosObject.Add("height", CosmosNumber.Create(72));
            patchableCosmosObject.Remove("age");
            patchableCosmosObject.Replace("name", CosmosString.Create("Alice"));
            Assert.AreEqual("Alice", ((CosmosString)patchableCosmosObject["name"].ToCosmosElement()).Value);
            Assert.AreEqual(72, ((CosmosNumber)patchableCosmosObject["height"].ToCosmosElement()).AsInteger().Value);
            Assert.IsFalse(patchableCosmosObject.ContainsKey("age"));

            // Convert it back into a CosmosElement for navigation
            CosmosObject patchedCosmosObjectWrapper = (CosmosObject)patchableCosmosObject.ToCosmosElement();
            Assert.AreEqual(2, patchedCosmosObjectWrapper.Count);
            Assert.AreEqual(72, ((CosmosNumber)patchedCosmosObjectWrapper["height"]).AsInteger().Value);
            Assert.AreEqual("Alice", ((CosmosString)patchedCosmosObjectWrapper["name"]).Value);

            // And then additional conversions should not change the data
            PatchableCosmosObject patchableCosmosObject2 = (PatchableCosmosObject)patchedCosmosObjectWrapper.ToPatchable();
            CosmosObject patchedCosmosObjectWrapper2 = (CosmosObject)patchableCosmosObject2.ToCosmosElement();
            Assert.AreEqual(2, patchedCosmosObjectWrapper.Count);
            Assert.AreEqual(72, ((CosmosNumber)patchedCosmosObjectWrapper["height"]).AsInteger().Value);
            Assert.AreEqual("Alice", ((CosmosString)patchedCosmosObjectWrapper["name"]).Value);
        }
        #endregion
    }
}