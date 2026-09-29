using ThreeSouls.Interface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ThreeSouls.Data;

namespace ThreeSouls.Manager
{
	public class TimeManager : MonoBehaviour, IManager
	{
		public static TimeManager Get() { return GameManager.Instance.Get<TimeManager>(); }

		private readonly Dictionary<string, TimeData> TimeContainer = new();
		private IEnumerator TimeCoroutine;

		public TimeManager SetTime(string key, OnTimeDelegate onTimeEvent)
		{
            if (!TimeContainer.TryGetValue(key, out var timeData))
            {
                timeData = new TimeData();
                timeData.OnTimeEventHandler += onTimeEvent;

                TimeContainer.Add(key, timeData);
            }
            else
            {
                timeData.OnTimeEventHandler += onTimeEvent;
            }

			return this;
		}

		public TimeManager SetReferenceSeconds(string key, double referenceSeconds)
		{
			if (IsValid(key))
			{
				TimeContainer[key].ReferenceSeconds = referenceSeconds;
			}

			return this;
		}

		public TimeManager ReleaseWhenSceneChanges(string key, bool isReleaseSceneChanges)
		{
			if (IsValid(key))
			{
				TimeContainer[key].IsReleaseSceneChange = isReleaseSceneChanges;
			}

			return this;
		}

		public void RemoveTime(string key)
        {
            if (!IsValid(key))
            {
                return;
            }
            
            TimeContainer[key].TimeStopwatch.Stop();
            TimeContainer[key].TimeStopwatch = null;
            TimeContainer[key].Release();

            TimeContainer.Remove(key);
        }

		public void TimeStart(string key)
        {
            if (!IsValid(key))
            {
                return;
            }
            
            if (TimeCoroutine == null)
            {
                TimeCoroutine = CoTimeCoroutine();
                StartCoroutine(TimeCoroutine);
            }

            TimeContainer[key].TimeStopwatch.Start();
        }

		public void TimeStop(string key)
		{
			if (IsValid(key))
			{
				TimeContainer[key].TimeStopwatch.Stop();
			}
		}

		public void TimeReset(string key)
		{
			if (IsValid(key))
			{
				TimeContainer[key].TimeStopwatch.Reset();
			}
		}

		private void OnApplicationPause(bool pause)
		{
			if (pause == true)
			{

			}
			else
			{

			}
		}

		private IEnumerator CoTimeCoroutine()
		{
			while (true)
			{
				using var enumerator = TimeContainer.GetEnumerator();

				while (enumerator.MoveNext())
                {
                    if (!enumerator.Current.Value.TimeStopwatch.IsRunning)
                    {
                        continue;
                    }

                    double elapsedSeconds;

                    if (enumerator.Current.Value.ReferenceSeconds > 0)
                    {
                        elapsedSeconds = enumerator.Current.Value.ReferenceSeconds - enumerator.Current.Value.TimeStopwatch.ElapsedMilliseconds / 1000f;

                        if (elapsedSeconds <= 0)
                        {
                            elapsedSeconds = 0;
                            enumerator.Current.Value.TimeStopwatch.Stop();
                        }
                    }
                    else
                    {
                        elapsedSeconds = enumerator.Current.Value.TimeStopwatch.ElapsedMilliseconds / 1000f;
                    }

                    enumerator.Current.Value.Action(elapsedSeconds);
                }

				yield return null;
			}
		}

		private bool IsValid(string key)
        {
            if (TimeContainer.ContainsKey(key))
			{
				return true;
			}

            Debug.LogError($"Not found time manager key [Key : {key}]");
            return false;
        }

		public void OnSceneChanged()
		{
			var enumerator = TimeContainer.GetEnumerator();

			while (enumerator.MoveNext())
			{
				if (enumerator.Current.Value.IsReleaseSceneChange)
				{
					enumerator.Current.Value.TimeStopwatch.Stop();
				}
			}
		}

		public void Initialize() { }

		public void Release() { }
	}
}
