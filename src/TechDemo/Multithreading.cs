namespace TechDemo;

public static class Multithreading {
    public static Task Continue<TResult>(this Task<TResult> task, Action a){
		return task.ContinueWith((_)=> a());
	}
}