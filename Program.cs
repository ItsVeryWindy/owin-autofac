using Autofac;
using Autofac.Core.Lifetime;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var run = WebApp.Start<Startup>("http://*:5560"))
            {
                Console.ReadLine();
            }
        }
    }

    public class Startup
    {
        public static void Configuration(IAppBuilder app)
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<Test>().InstancePerRequest();

            var container = builder.Build();

            app.UseLifetimeScope(container);

            app.UseAutofac<FirstMiddleware>();
            app.UseAutofac<FirstMiddleware>();
            app.UseAutofac<LastMiddleware>();
        }
    }

    public class Test
    {
        public string Hello { get; set; }
    }

    public class FirstMiddleware : OwinMiddleware
    {
        private Test _test;

        public FirstMiddleware(Test test, OwinMiddleware next) : base(next)
        {
            _test = test;
        }

        public override Task Invoke(IOwinContext context)
        {
            _test.Hello = "hello";

            return Next.Invoke(context);
        }
    }

    public class LastMiddleware : OwinMiddleware
    {
        private Test _test;

        public LastMiddleware(Test test, OwinMiddleware next): base(next)
        {
            _test = test;
        }

        public override Task Invoke(IOwinContext context)
        {
            return context.Response.WriteAsync(_test.Hello);
        }
    }

    public static class AppBuilderExtensions
    {
        public static void UseLifetimeScope(this IAppBuilder app, IContainer container)
        {
            app.Use<LifetimeScopeMiddleware>(container);
        }

        public static void UseAutofac<T>(this IAppBuilder app) where T : OwinMiddleware
        {
            app.Use<AutofacResolverMiddleware<T>>();
        }
    }

    public class AutofacResolverMiddleware<T> : OwinMiddleware
        where T : OwinMiddleware
    {
        public AutofacResolverMiddleware(OwinMiddleware next) : base(next)
        {

        }

        public async override Task Invoke(IOwinContext context)
        {
            var scope = context.Get<ILifetimeScope>("DI");

            using (var innerScope = scope.BeginLifetimeScope(b => {
                b.RegisterInstance(Next);
                b.RegisterType<T>();
                }))
            {
                var instance = innerScope.Resolve<T>();

                await instance.Invoke(context);
            }
        }
    }

    public class LifetimeScopeMiddleware : OwinMiddleware
    {
        IContainer _container;

        public LifetimeScopeMiddleware(OwinMiddleware next, IContainer container) : base(next)
        {
            _container = container;
        }

        public async override Task Invoke(IOwinContext context)
        {
            using (var scope = _container.BeginLifetimeScope(MatchingScopeLifetimeTags.RequestLifetimeScopeTag, builder =>
            {
                builder.RegisterInstance(context).As<IOwinContext>();
            }))
            {
                context.Set("DI", scope);

                await Next.Invoke(context);
            }
        }
    }
}
