﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NSpec.Domain.Formatters;
using System.Threading.Tasks;

namespace NSpec.Domain
{
    public class Context
    {
        public void RunBefores(nspec instance)
        {
            // parent chain

            RecurseAncestors(c => c.RunBefores(instance));

            // class (method-level)

            if (BeforeInstance != null && BeforeInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'before_each' set, please pick one of the two");
            }

            BeforeInstance.SafeInvoke(instance);

            BeforeInstanceAsync.SafeInvoke(instance);

            // context-level

            if (Before != null && BeforeAsync != null)
            {
                throw new ArgumentException("A single context cannot have both a 'before' and an 'beforeAsync' set, please pick one of the two");
            }

            Before.SafeInvoke();

            BeforeAsync.SafeInvoke();
        }

        void RunBeforeAll(nspec instance)
        {
            // context-level

            if (BeforeAll != null && BeforeAllAsync != null)
            {
                throw new ArgumentException("A single context cannot have both a 'beforeAll' and an 'beforeAllAsync' set, please pick one of the two");
            }

            BeforeAll.SafeInvoke();

            BeforeAllAsync.SafeInvoke();

            // class (method-level)

            if (BeforeAllInstance != null && BeforeAllInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'before_all' set, please pick one of the two");
            }

            BeforeAllInstance.SafeInvoke(instance);

            BeforeAllInstanceAsync.SafeInvoke(instance);
        }

        public void RunActs(nspec instance)
        {
            // parent chain

            RecurseAncestors(c => c.RunActs(instance));

            // class (method-level)

            if (ActInstance != null && ActInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'act_each' set, please pick one of the two");
            }

            ActInstance.SafeInvoke(instance);

            ActInstanceAsync.SafeInvoke(instance);

            // context-level

            if (Act != null && ActAsync != null)
            {
                throw new ArgumentException("A single context cannot have both an 'act' and an 'actAsync' set, please pick one of the two");
            }

            Act.SafeInvoke();

            ActAsync.SafeInvoke();
        }

        public void RunAfters(nspec instance)
        {
            // context-level

            if (After != null && AfterAsync != null)
            {
                throw new ArgumentException("A single context cannot have both an 'after' and an 'afterAsync' set, please pick one of the two");
            }

            After.SafeInvoke();

            AfterAsync.SafeInvoke();

            // class (method-level)

            if (AfterInstance != null && AfterInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'after_each' set, please pick one of the two");
            }

            AfterInstance.SafeInvoke(instance);

            AfterInstanceAsync.SafeInvoke(instance);

            // parent chain

            RecurseAncestors(c => c.RunAfters(instance));
        }

        public void RunAfterAll(nspec instance)
        {
            // context-level

            if (AfterAll != null && AfterAllAsync != null)
            {
                throw new ArgumentException("A single context cannot have both an 'afterAll' and an 'afterAllAsync' set, please pick one of the two");
            }

            AfterAll.SafeInvoke();

            AfterAllAsync.SafeInvoke();

            // class (method-level)

            if (AfterAllInstance != null && AfterAllInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'after_all' set, please pick one of the two");
            }

            AfterAllInstance.SafeInvoke(instance);

            AfterAllInstanceAsync.SafeInvoke(instance);
        }

        public void AddExample(ExampleBase example)
        {
            example.Context = this;

            example.Tags.AddRange(Tags);

            Examples.Add(example);

            example.Pending |= IsPending();
        }

        public IEnumerable<ExampleBase> AllExamples()
        {
            return Contexts.Examples().Union(Examples);
        }

        public bool IsPending()
        {
            return isPending || (Parent != null && Parent.IsPending());
        }

        public IEnumerable<ExampleBase> Failures()
        {
            return AllExamples().Where(e => e.Exception != null);
        }

        public void AddContext(Context child)
        {
            child.Level = Level + 1;

            child.Parent = this;

            child.Tags.AddRange(child.Parent.Tags);

            Contexts.Add(child);
        }

        public virtual void Run(ILiveFormatter formatter, bool failFast, nspec instance = null)
        {
            if (failFast && Parent.HasAnyFailures()) return;

            var nspec = savedInstance ?? instance;

            bool itShouldRunAnyExample = AllExamples().Any(e => e.ShouldNotSkip(nspec.tagsFilter));

            if (itShouldRunAnyExample) RunAndHandleException(RunBeforeAll, nspec, ref Exception);

            //intentionally using for loop to prevent collection was modified error in sample specs
            for (int i = 0; i < Examples.Count; i++)
            {
                var example = Examples[i];
                if (failFast && example.Context.HasAnyFailures()) return;

                Exercise(example, nspec);

                if (example.HasRun && !alreadyWritten)
                {
                    WriteAncestors(formatter);
                    alreadyWritten = true;
                }

                if (example.HasRun) formatter.Write(example, Level);
            }

            Contexts.Do(c => c.Run(formatter, failFast, nspec));

            if (itShouldRunAnyExample) RunAndHandleException(RunAfterAll, nspec, ref Exception);
        }

        public virtual void Build(nspec instance = null)
        {
            instance.Context = this;

            savedInstance = instance;

            Contexts.Do(c => c.Build(instance));
        }

        public string FullContext()
        {
            return Parent != null ? Parent.FullContext() + ". " + Name : Name;
        }

        public void RunAndHandleException(Action<nspec> action, nspec nspec, ref Exception exceptionToSet)
        {
            try
            {
                action(nspec);
            }
            catch (TargetInvocationException invocationException)
            {
                if (exceptionToSet == null) exceptionToSet = nspec.ExceptionToReturn(invocationException.InnerException);
            }
            catch (Exception exception)
            {
                if (exceptionToSet == null) exceptionToSet = nspec.ExceptionToReturn(exception);
            }
        }

        public void Exercise(ExampleBase example, nspec nspec)
        {
            if (example.ShouldSkip(nspec.tagsFilter)) return;

            RunAndHandleException(RunBefores, nspec, ref Exception);

            RunAndHandleException(RunActs, nspec, ref Exception);

            RunAndHandleException(example.Run, nspec, ref example.Exception);

            RunAndHandleException(RunAfters, nspec, ref Exception);

            example.AssignProperException(Exception);
        }

        public virtual bool IsSub(Type baseType)
        {
            return false;
        }

        public nspec GetInstance()
        {
            return savedInstance ?? Parent.GetInstance();
        }

        public IEnumerable<Context> AllContexts()
        {
            return new[] { this }.Union(ChildContexts());
        }

        public IEnumerable<Context> ChildContexts()
        {
            return Contexts.SelectMany(c => new[] { c }.Union(c.ChildContexts()));
        }

        public bool HasAnyFailures()
        {
            return AllExamples().Any(e => e.Failed());
        }

        public bool HasAnyExecutedExample()
        {
            return AllExamples().Any(e => e.HasRun);
        }

        public void TrimSkippedDescendants()
        {
            Contexts.RemoveAll(c => !c.HasAnyExecutedExample());

            Examples.RemoveAll(e => !e.HasRun);

            Contexts.Do(c => c.TrimSkippedDescendants());
        }

        void RecurseAncestors(Action<Context> ancestorAction)
        {
            if (Parent != null) ancestorAction(Parent);
        }

        void WriteAncestors(ILiveFormatter formatter)
        {
            if (Parent == null) return;

            Parent.WriteAncestors(formatter);

            if (!alreadyWritten) formatter.Write(this);

            alreadyWritten = true;
        }

        public Context(string name = "", string tags = null, bool isPending = false)
        {
            Name = name.Replace("_", " ");
            Examples = new List<ExampleBase>();
            Contexts = new ContextCollection();
            Tags = Domain.Tags.ParseTags(tags);
            this.isPending = isPending;
        }

        public string Name;
        public int Level;
        public List<string> Tags;
        public List<ExampleBase> Examples;
        public ContextCollection Contexts;
        public Action Before, Act, After, BeforeAll, AfterAll;
        public Action<nspec> BeforeInstance, ActInstance, AfterInstance, BeforeAllInstance, AfterAllInstance;
        public Func<Task> BeforeAsync, ActAsync, AfterAsync, BeforeAllAsync, AfterAllAsync;
        public Action<nspec> BeforeInstanceAsync, ActInstanceAsync, AfterInstanceAsync, BeforeAllInstanceAsync, AfterAllInstanceAsync;
        public Context Parent;
        public Exception Exception;

        nspec savedInstance;
        bool alreadyWritten, isPending;
    }
}