/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using Android.App;
using Android.Content;
using Android.OS;
using Java.Lang;
using Java.Security;
using System.Threading.Tasks;

namespace keepass2android
{
    /// <summary>
        /// Class to run a task while a progress dialog is shown
        /// </summary>
        public class BlockingOperationRunner
	{
        //for handling Activity recreation situations, we need access to the currently active task. It must hold that there is no more than one active task.
	    private static BlockingOperationRunner _currentTask = null;

	    public static void SetNewActiveActivity(Activity activeActivity)
	    {
	        if (_currentTask != null)
	        {
	            _currentTask.ActiveActivity = activeActivity;
	        }
	    }
	    public static void RemoveActiveActivity(Activity activity)
	    {
	        if ((_currentTask != null) && (_currentTask._activeActivity == activity))
	            _currentTask.ActiveActivity = null;

	    }

        public Context ActiveActivity
	    {
	        get { return _activeActivity; }
	        private set
	        {
                _activeActivity = value;
	            
	            if (_activeActivity != null)
	            {
	                SetupProgressDialog(_app);
	                _progressDialog.Show();
                }
	        }
	    }

        private readonly Handler _handler;
		private readonly OperationWithFinishHandler _task;
		private IProgressDialog _progressDialog;
        private readonly IKp2aApp _app;
        private Java.Lang.Thread _thread;
	    private Context _activeActivity;
	    private ProgressDialogStatusLogger _progressDialogStatusLogger;

	    public BlockingOperationRunner(IKp2aApp app, OperationWithFinishHandler task)
		{
		    _activeActivity = app.ActiveContext;
			_task = task;
			_handler = app.UiThreadHandler;
            _app = app;
			
			SetupProgressDialog(app);

		    // Set code to run when this is finished
            _task.operationFinishedHandler = new AfterTask(app, task.operationFinishedHandler, _handler, this);
		    
		    _task.SetStatusLogger(_progressDialogStatusLogger);
			
			
		}

	    private void SetupProgressDialog(IKp2aApp app)
	    {
	        string currentMessage = "Initializing...";
	        string currentSubmessage = "";

	        if (_progressDialogStatusLogger != null)
	        {
	            currentMessage = _progressDialogStatusLogger.Message;
	            currentSubmessage = _progressDialogStatusLogger.SubMessage;
	        }

	        if (_progressDialog != null)
	        {
	            var pd = _progressDialog;
                app.UiThreadHandler.Post(() =>
                {
                    pd.Dismiss();
                });
	        }

            // Show process dialog
            _progressDialog = app.CreateProgressDialog(_activeActivity);
	        _progressDialog.SetTitle(_app.GetResourceString(UiStringKey.progress_title));
            _progressDialogStatusLogger = new ProgressDialogStatusLogger(_app, _handler, _progressDialog);
	        _progressDialogStatusLogger.UpdateMessage(currentMessage);
	        _progressDialogStatusLogger.UpdateSubMessage(currentSubmessage);
	    }

	    public void Run(bool allowOverwriteCurrentTask = false)
		{
		    if ((!allowOverwriteCurrentTask) && (_currentTask != null))
		        throw new System.Exception("Cannot start another BlockingOperationRunner while BlockingOperationRunner is already running! " + _task.GetType().Name + "/" + _currentTask._task.GetType().Name);
		    _currentTask = this;

            // Show process dialog
            _progressDialog.Show();
			
			
			// Start Thread to Run task
			_thread = new Java.Lang.Thread(_task.Run);
			_thread.Start();
		}

		public void JoinWorkerThread()
		{
			_thread.Join();
		}
		
		private class AfterTask : OnOperationFinishedHandler {
			readonly BlockingOperationRunner _blockingOperationRunner;

			public AfterTask (IActiveContextProvider app, OnOperationFinishedHandler operationFinishedHandler, Handler handler, BlockingOperationRunner pt): base(app, operationFinishedHandler, handler)
			{
				_blockingOperationRunner = pt;
			}

			public override void Run() {
				base.Run();

				if (Handler != null) //can be null in tests
				{
					// Remove the progress dialog
					Handler.Post(delegate
					{
					    _blockingOperationRunner._progressDialog.Dismiss();
					});
				}
				else
				{
				    _blockingOperationRunner._progressDialog.Dismiss();
				}
			    _currentTask = null;

			}
			
		}

	    
	}
}

