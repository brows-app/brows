---
layout: post
title: Configuring .NET apps without JSON or XML
author: the Brows app developers
excerpt: XML and JSON have long been the configuration formats of choice in the .NET ecosystem. For something entirely different, continue reading.
tags: Domore Domore.Conf Conf Config Configuration
---
XML and JSON have long been the configuration formats of choice in the .NET ecosystem.
For something entirely different, continue reading.

**[Domore.Conf](https://github.com/kyourek/domore)** configures POCO classes with a syntax that resembles a programming
language but is less strict in its use.

> This is the first is a series of posts that describes how [Brows]({{ site.github.repository_url }}) leverages
> simple text for configuration and data.

Consider the following class.
```csharp
class MyConfig {
    public string Theme { get; set; }
    public DayOfWeek WeekendStartsOn { get; set; }
}
```
A configuration file for that class might look like this.
```
My config . Theme = dark
my config . Weekend starts on = Saturday
```
If that configuration file exists alongside an application's *.exe* with the same name as
the *.exe* but with different extension *.conf*, an instance may be configured by the following.
```csharp
var obj = new MyConfig();
Conf.Configure(obj);
```
I'm sure you can guess what the output will be.
```csharp
Console.WriteLine(obj.Theme);           // dark
Console.WriteLine(obj.WeekendStartsOn); // Saturday
```

> If the file-naming convention described above was confusing, here's an example that should clear
> things up. An executable named **my-process.exe** would have a configuration file in the same
> directory with the name **my-process.conf**.

--------------------------------------------------------------
### So far things seem pretty simple. Let's make them simpler.
Change the configuration file to look like this:
```csharp
Theme = dark
Weekend starts on = Saturday
```
And configure the object with a blank key.
```csharp
Conf.Configure(obj, key: "");
```
If a key is not provided, or is `null`, the name of the type will be used to prefix configuration 
properties. If a blank key is passed (`string.Empty`), each line is a potential configuration
property.

That behavior allows some interesting conveniences. Suppose the class above is subclassed.
```csharp
class PartyAnimal : MyConfig { }
class CollegeStudent : PartyAnimal { }
```
And the configuration changes to this:
```
Theme = dark
Weekend starts on = Friday
college student . weekend starts on = thursday
```
Instances of each class may be configured by either or both a blank and `null` key.
```csharp
var pa = Conf.Configure(new PartyAnimal(), key: "");
var cs = Conf.Configure(new CollegeStudent(), key: "");
Conf.Configure(cs);
```
The college student is configured twice, first with a blank key to gather generic configuration for
all instances of `MyConfig`, then with a key matching the name of the type. The output is as follows.
```
Console.WriteLine(pa.Theme);           // dark
Console.WriteLine(pa.WeekendStartsOn); // Friday
Console.WriteLine(cs.Theme);           // dark
Console.WriteLine(cs.WeekendStartsOn); // Thursday
```
Both the party animal and the college student got the same configuration for their theme, but the
college student has more specific configuration for the starting day of his weekend.

> Notice that a new instance of each type was passed to `Conf.Configure`.
> That same instance is returned from the method.

------------------------------------------------
### Let's set some rules that we've seen so far.
 - Whitespace in configuration keys is ignored.
 - Configuration keys are case-insensitive.
 - Configuration key parts are separated by a dot (`.`).

------------------------------------------------

Now let's clean up that college student and send him to class.
```csharp
class CollegeStudent {
    public List<DayOfWeek> ClassSchedule { get; set; }
}
```
The class schedule could be configured with this *.conf* file.
```
student.class schedule[0] = monday
student.class schedule[1] = wednesday
student.class schedule[2] = friday
```
But that's a little clunky. It's more readable and easier to maintain like this.
```
student.class schedule = monday, Wednesday, Friday
```

> In the above example, neither a blank key nor the type name was used as the key.
> Rather, `student` may be used by calling `Conf.Configure` with an arbitrary
> key, i.e.:
>
>  `Conf.Configure(new CollegeStudent(), key: "student")`.

------------------------------------------------------
### College isn't *that* easy. It's a little more complex.
```csharp
class CollegeProfessor {
    public string Name { get; set; }
}
class CollegeClass { 
    public CollegeProfessor Professor { get; set; }
    public List<DayOfWeek> Schedule { get; set; }
    public TimeSpan Time { get; set; }
}
class CollegeStudent {
    public Dictionary<string, CollegeClass> Class { get; set; }
}
```
That student's schedule can be configured with this *.conf* file by calling
`Conf.Configure(new CollegeStudent(), key: "")`.
```
class[Chemistry 101].time           = 8:00
class[Chemistry 101].schedule       = Monday, Wednesday, Friday
class[Chemistry 101].professor.name = Marie Curie
class[ Physics 200 ].time           = 10:30
class[ Physics 200 ].schedule       = Tuesday, Wednesday
class[ Physics 200 ].professor.name = Georges Lemaître
```
> Dictionary keys follow a few rules different from standard keys.
> - Whitespace before and after the key is ignored.
> - Whitespace *within* the key is respected.
> - Case is respected. To ignore the case of a dictionary's keys, create it with a
>   case-insensitive comparer.
>   - `new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)`.

-------------------------------------------------
### That's it for now, but there's more to cover.
In a future post, I'll go over custom configuration parsing, attributes for configuring
your configuration, and reversing the whole process to convert a POCO class into a
configuration file.
