﻿using System;
using System.Collections.Generic;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Core;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.NodeWrappers;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using eXpand.ExpressApp.SystemModule;
using TypeMock.ArrangeActAssert;
using eXpand.ExpressApp.Core;
using System.Linq;

namespace eXpand.Tests.eXpand.WorldCreator
{

    internal class TestAppLication<TObject> : IApplicationHandler<TObject>, IArtifactHandler<TObject>, IViewCreationHandler, IFrameCreationHandler where TObject:BaseObject
    {
        ObjectSpace _objectSpace;
        ApplicationNodeWrapper _applicationNodeWrapper;
        XafApplication _xafApplication;
        View _view;
        Frame _frame;
        TObject _currentObject;

        public IArtifactHandler<TObject> Setup(Action<XafApplication> created,Action<TObject> action)
        {
            _applicationNodeWrapper = new ApplicationNodeWrapper(new Dictionary(new DictionaryNode(ApplicationNodeWrapper.NodeName), Schema.GetCommonSchema()));
            _applicationNodeWrapper.Load(typeof(TObject));

            var objectSpaceProvider = new ObjectSpaceProvider(new MemoryDataStoreProvider());
            _objectSpace = objectSpaceProvider.CreateObjectSpace();

            _currentObject = (TObject) _objectSpace.CreateObject(typeof (TObject));
            if (action != null) action.Invoke(_currentObject);

            _xafApplication = Isolate.Fake.Instance<XafApplication>();
            if (created != null) created.Invoke(_xafApplication);
            return this;
        }


        public IArtifactHandler<TObject> Setup()
        {
            return Setup(null, null);
        }



        TObject IArtifactHandler<TObject>.CurrentObject {
            get { return _currentObject; }
        }

        UnitOfWork IArtifactHandler<TObject>.UnitOfWork {
            get { return (UnitOfWork) _objectSpace.Session; }
        }

        IViewCreationHandler IArtifactHandler<TObject>.WithArtiFacts(Func<Type[]> func){
            XafTypesInfo.Instance.RegisterEntity(typeof(TObject));
//            XafTypesInfo.Instance.LoadTypes(typeof(SystemModule).Assembly);
//            XafTypesInfo.Instance.LoadTypes(typeof (SystemWindowsFormsModule).Assembly);
            XafTypesInfo.Instance.LoadTypes(typeof(eXpandSystemModule).Assembly);
            if (func != null)
                foreach (var type in func.Invoke()){
                    XafTypesInfo.Instance.LoadTypes(type.Assembly);
                }
            return this;

        }



        IViewCreationHandler IArtifactHandler<TObject>.WithArtiFacts()
        {
            return ((IArtifactHandler<TObject>)this).WithArtiFacts(null);
        }
        IFrameCreationHandler IViewCreationHandler.CreateListView() {
            return ((IViewCreationHandler)this).CreateListView(true, null);
        }

        IFrameCreationHandler IViewCreationHandler.CreateListView(bool isRoot, Action<ListView> created) {
            ListViewInfoNodeWrapper listViewInfoNodeWrapper = _applicationNodeWrapper.Views.GetListViews(typeof(TObject))[0];
            var listEditor = Isolate.Fake.Instance<ListEditor>();
            Isolate.WhenCalled(() => listEditor.RequiredProperties).WillReturn(new string[0]);
            Isolate.WhenCalled(() => listEditor.Model).WillReturn(listViewInfoNodeWrapper);
            _view = new ListView(new CollectionSource(_objectSpace, typeof (TObject)), listEditor, isRoot, _xafApplication);
            
            Isolate.WhenCalled(() => _view.SynchronizeWithInfo()).IgnoreCall();
            _view.SetInfo(listViewInfoNodeWrapper.Node);
            if (created != null) created.Invoke((ListView) _view);
            return this;
        }

        IFrameCreationHandler IViewCreationHandler.CreateDetailView() {

            foreach (var info in _currentObject.ClassInfo.CollectionProperties.OfType<XPMemberInfo>()){
                _applicationNodeWrapper.Load(info.CollectionElementType.ClassType);
                ((IViewCreationHandler)this).CreateListView(false, null);
            }
            _view = new DetailView(_objectSpace, _currentObject, _xafApplication, true);
            Isolate.WhenCalled(() => _view.SynchronizeWithInfo()).IgnoreCall();
            _view.SetInfo(_applicationNodeWrapper.Views.GetDetailViews(typeof(TObject))[0].Node);
            
            return this;
        }

        void IFrameCreationHandler.CreateFrame(Action<Frame> created) {
            var controllersManager = new ControllersManager();
            controllersManager.CollectControllers(info => true);
            IList<Controller> collectControllers = controllersManager.CreateControllers(typeof (Controller));
            _frame = new Frame(_xafApplication, TemplateContext.View, collectControllers);
            if (_view == null)
                ((IViewCreationHandler) this).CreateDetailView();
            _frame.SetView(_view);
            if (created != null) created.Invoke(_frame);
        }

        void IFrameCreationHandler.CreateFrame() {
            ((IFrameCreationHandler) this).CreateFrame(null);
        }

//        void IControllerActivateHandler.ActivateControllers(Action<Controller> action) {
//            IEnumerable<Controller> controllers = _frame.Controllers;
//            foreach (ViewController controller in controllers.Where(controller => typeof(ViewController).IsAssignableFrom(controller.GetType()))) {
//                ViewController controller1 = controller;
//                Isolate.WhenCalled(() => controller1.Frame).WillReturn(_frame);
//
//                controller.Application = _xafApplication;
//                if (!controller.Active) {
//                    controller.SetView(_view);
//                    if (action != null) action.Invoke(controller);
//                }
//            }
//        }

        //void IControllerActivateHandler.ActivateControllers() {
        //    ((IControllerActivateHandler) this).ActivateControllers(null);
        //}
    }

    internal interface IApplicationHandler<TObject>
    {
        IArtifactHandler<TObject> Setup(Action<XafApplication> action1,Action<TObject> action);
        IArtifactHandler<TObject> Setup();
    }

    internal interface IArtifactHandler<TObject>
    {
        TObject CurrentObject { get; }
        UnitOfWork UnitOfWork { get; }
        IViewCreationHandler WithArtiFacts(Func<Type[]> func);
        IViewCreationHandler WithArtiFacts();
    }

    internal interface IFrameCreationHandler {
        void CreateFrame(Action<Frame> created);
        void CreateFrame();
    }
    internal interface IViewCreationHandler {
        IFrameCreationHandler CreateListView();
        IFrameCreationHandler CreateListView(bool isRoot, Action<ListView> created);
        IFrameCreationHandler CreateDetailView();
        
    }


    internal interface IControllerActivateHandler {
        void ActivateControllers(Action<Controller> action);
        void ActivateControllers();
    }
//    public class RunControllers : IInitializationHandler
//    {
//        static RunControllers _instance;
//
//        public static IInitializationHandler ForDetailView(Nesting nested) {
//            return _instance;
//        }
//
//        public static IInitializationHandler ForListView(Nesting nested) {
//            
//        }
//        public static IInitializationHandler ForListView(Nesting nested,Action<ListView> created)
//        {
//            return _instance;
//        }
//
//        IInitializationObjectSpaceproviderHandler IInitializationArtifactsHandler.WithArtifacts(Action<Assembly[]>  action) {
//        }
//
//        IInitializationObjectSpaceproviderHandler IInitializationArtifactsHandler.WithArtifacts() {
//            
//        }
//
//        IActivationHandler IInitializationObjectSpaceproviderHandler.WithObjectSpaceProvider() {
//            
//        }
//
//        void IActivationHandler.WhenActivating(Func<ViewController, bool> action) {
//            
//        }
//
//        public static RunControllers Instance {
//            get {
//                if (_instance == null)
//                    _instance = new RunControllers();
//                return _instance;
//            }
//        }
//
//        public void ForObject<T>() {
//            
//        }
//    }
//
//    public interface IInitializationHandler : IInitializationObjectSpaceproviderHandler, IInitializationArtifactsHandler,IActivationHandler
//    {
//        
//    }
//    public interface IActivationHandler {
//        void WhenActivating(Func<ViewController, bool> action);
//    }
//    public interface IInitializationObjectSpaceproviderHandler {
//        IActivationHandler WithObjectSpaceProvider();
//    }
//    public interface IInitializationArtifactsHandler {
//        IInitializationObjectSpaceproviderHandler WithArtifacts();
//    }
}
