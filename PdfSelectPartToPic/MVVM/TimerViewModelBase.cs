using System;
using System.Collections.Generic;

namespace PdfSelectPartToPic.MVVM
{
    public abstract class TimerViewModelBase : ViewModelBase, ITimerViewModelBase
	{
        PeriodicJob _timer;

        static List<TimerViewModelBase> _lstAll = new List<TimerViewModelBase>();

        public static void StopAll()
        {
            foreach (TimerViewModelBase vm in _lstAll)  
                vm.Stop();
        }


        public TimerViewModelBase(string name)
        {
            _timer = new PeriodicJob(1000, this.OnTimer, "UIUpdaterThread - " + name, false, true);

            _lstAll.Add(this);
        }

        //
        protected virtual bool OnTimer() 
        {
            try
            {
                Poll();               
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }

            return true;
        }
        
        public void Start()
        {
            _timer.Start();
        }


        public void Stop()
        {
            _timer.Stop();
        }

        public void Dispose()
        {
            Stop();
        }
        
        protected abstract void Poll();

        public virtual void UpdateData() { }

        public virtual void EnableTimer(bool enable)
        {
            if (enable) _timer.Start();
            else _timer.Pause();
        }
    }

	public interface ITimerViewModelBase
	{
		void EnableTimer(bool enable);
	}
}
