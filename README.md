# Changes: PTX file support
Now you can load kernels files written in PTX assembly language and view SASS disassembly on-the-fly for debugging, learning or testing different compiler settings. These files are to be used in PTX or CUBIN format with the CUDA Driver API.

# CudaPAD
## CudaPAD is a PTX/SASS viewer for NVIDIA Cuda kernels and provides an on-the-fly view of the assembly.
See the Article on CodeProject at http://www.codeproject.com/Articles/999744/CudaPAD for more details.

![10_intro](https://cloud.githubusercontent.com/assets/10804507/21625199/711f6e1e-d1bf-11e6-888f-d30ddf585231.png)

<div>_Requirements: Visual Studio 2010-2015 and Cuda 7.0/7.5/8.0._</div>

## What is CudaPAD?

CudaPAD aids in the optimizing and understanding of nVidia’s Cuda kernels by displaying an on-the-fly view of the PTX/SASS that make up the GPU kernel. CudaPAD simply shows the PTX/SASS output, however it has several visual aids to help understand how minor code tweaks or compiler options can affect the PTX/SASS.

What is PTX or SASS anyway? NVidia’s PTX is an intermediate language for NVidia GPU’s. It is more closely tied to pure GPU assembly(SASS) but slightly abstracted. PTX is less tied to the specific hardware or a hardware generation which makes it more useful in most cases when compared to assembly. One item it abstracts is physical register numbers which makes it easier to use then assembly. PTX instructions are usually translated into one or more actual SASS hardware instructions. SASS is hardcore assembly. It is what the GPU actually runs and is directly translated into machine code. Viewing SASS code is more difficult but it does show exactly what the GPU will do. As mentioned, SASS code also works with the registers directly so there is more control where registers are stored but it’s another item that the programmer needs to keep track of and makes SASS more difficult to work with.

Often when programming in Cuda, there is a need to view what a kernel’s PTX/SASS might look like and CudaPAD helps with this. There might be a need to view PTX/SASS for debugging, understanding what’s happening, to squeezing a little more performance out of a kernel, or just for curiosity. To use the application, simply type or paste a kernel in the left panel and then the right panel will display the corresponding disassembly information. Visual informational aids like visual Cuda-to-PTX code matching lines, PTX cleanup, WinDiff, and quick register highlighting are built-in to help make the PTX easily to follow. Other on-the-fly information is also displayed like register counts, memory usage, and error information.

With any piece of code, there are often several ways to perform the same thing. Sometimes, just modifying a line or two will lead to different machine instructions with better registers and memory usage. Have fun and make some changes to a kernel in the left window and watch how the PTX/SASS changes on the right.

Just as a quick note. CudaPAD does not run any code. CudaPAD is only for viewing PTX, SASS, and register/memory usage.

## Background

Like most of my projects, this one was grown out of a personals need. For some algorithms I develop, GPU efficiency is important. One way to help with this is by understanding the low-level mechanics and making any necessary adjustments. Before creating this app, I would often get in this loop where I would write a performance critical kernel then view the PTX/SASS over and over using command line tools. Doing this repetitively was time consuming so I decided to build a quick C# app that would automate the process.

It started out as a simple app that would take a kernel in the left window and then output the PTX to the right side window. This was accomplished by basically running the same command line tools as before, mainly nvcc.exe, but now in an automated fashion in the background. I got carried away however and within a short period of time I started adding several features including automatic re-compiling, WinDiff, visual code lines markers, compile errors, and register/memory usage.

AMD used to have a similar tool for Brooke++ and this gave me the idea of having the two window app back in 2009 when I first built it. Basically the tool had a left window where a Brook+ kernel could be added and a right window where the assembly would output to. A button could be clicked to update the output window. AMD has had a couple of these over the years but it has since been replaced with AMD’s CodeXL.

AMD’s CodeXL and NVidia’s NSight have since replaced many tools like these however CudaPAD still has its place for quick, on the fly viewing of low-level assembly and experimentation. Both CodeXL and NSight are professional grade free tools and are a must have for GPU developers.

## Using CudaPAD

### Requirements (updated 1/2017)

CudaPAD is simple to use. But before running it, make sure these system requirements are met:

*   Visual Studio 2010/2012/2013/2015 (Express/[Community](https://www.visualstudio.com/vs/community/) editions are okay)
*   [NVidia’s Cuda 7.0, 7.5 or 8.0](https://developer.nvidia.com/cuda-downloads)

A dedicated GPU is not required since we are only compiling code and not running anything.

If the requirements are met, then simply launch executable. When CudaPAD loads, it will have a sample kernel. The sample provides a quick place to start playing around or even a starting framework for a new kernel. Whenever the kernel on the left is edited, it will update the PTX or SASS on the right. If there is a compile error, it will show that near the bottom.

There are several features that can be enabled/disabled. All are on by default (also see [Features](#Features) section).

### PTX/SASS View Modes

Change the drop down textbox between PTX, SASS or SOURCE views.

**PTX view** – shows the PTX intermediate language output of the kernel. PTX is close to SASS hardware instructions but is slightly higher level and is less tied to a particular GPU generation. Usually PTX instructions translate directly to SASS however sometimes there are multiple SASS instructions per PTX instruction.

![20_ptx_view](https://cloud.githubusercontent.com/assets/10804507/21625200/71248f7a-d1bf-11e6-9a42-818a43d1099c.png)

**SASS view** – These are true assembly instructions. These types of instructions execute directly on the GPU. The amount of visual information supplied when viewing SASS is less then PTX – like the visual code lines do not show.

![30_sass_view](https://cloud.githubusercontent.com/assets/10804507/21625195/711cb016-d1bf-11e6-8706-d512d745ddbc.png)

**Raw code view** – This view is mostly for debugging CudaPAD itself. Behind the covers, this app does not re-compile after every change. It only re-compiles when the code is modified and not comments or whitespace. The raw code is a stripped down version of the real code. The reason this was added was because I did not want it to keep compiling when I was adding/editing comments or adding/removing whitespace. This would not be resource friendly and would also throw off the WinDiff feature.

In the background, CudaPAD simply compiles the kernels with Cuda tools. The Cuda compiler then in turn calls a C++ compiler like Visual Studio. So to run this CudaPAD, Cuda needs to be installed and most likely a C++ compiler like Visual Studio.

### Enabling/Disabling Features

![40_enabling-disabling__features](https://cloud.githubusercontent.com/assets/10804507/21625196/711dcaf0-d1bf-11e6-979c-555dc42a7cab.png)

Disabling the auto-compile is useful for making multiple changes before a compile. This can help show the changes in the diff (differencing) output over several changes. To do a manual compile, just click the green ‘start’ in the top right corner.

## Under the Hood

Let's take a look at how this application works. I will present what happens when the left window is edited. This triggers a recompile and then updates the right PTX/SASS window. Here it is in steps:

1.  User enters in some Cuda in the left window.
2.  The textbox change kicks off a short term timer. If the user should type in any more text before that timer finishes, then the timer is reset. This system prevents the compile process from firing on every keystroke and lets the user finish typing before it automatically starts.
3.  When the timer completes an event is raised. In this event, we check to see if there were any changes that would require a re-compile. Obviously, if a user is just editing some comments or adding/removing whitespace, then we don't need to recompile. If there are no "code" changes, then we stop here. In the dropdown box, CODE can be selected to see what this cleaned up code looks like.
4.  We save the Cuda textbox to a file. This will be needed later when the Cuda compiler compiles it.
5.  We then clear any lines on the screen as we are going to draw new ones soon.
6.  We then call a batch file that does most of the compiling. This batch file is generated based on the options selected in CudaPAD. If the user has the sm_35 architecture selected, then this option is appended to the nvcc line. If the user selects an optimization level of three, then -O3 is appended. If SASS output is requested, then the CuObjDump command is appended. Here is the batch file:
    1.  Perform some cleanup in the temp folder from the last time a compile was done.
    2.  Calls NVidia's cuda compiler with some options:
        `nvcc.exe -keep -cubin --generate-line-info ...`
        This command compiles the cuda file into a cubin file. (device code) We also use the `-keep` option and keep the ptx files as well as the `--generate-line-info` so we know the line numbers of the source file so we can draw the lines.
    3.  If SASS is selected from the dropdown, then we run _CuObjDump.exe_ to disassemble the cubin device file into SASS code.
    4.  Lastly, we capture any output messages from these commands to _info.txt_.
7.  Next, we fill the info textbox that has the registers and memory utilization information.
    1.  We extract this out from the output log _info.txt_ file we created from the batch file.
    2.  We then grab the global, constant, stack, and shared memory, byte counts, register spill information, register usage and general log information using RegEx.
    3.  This info is then formatted and displayed in the informational window.
8.  Next, any errors/warnings are captured from the _rtcof.dat_ file and are then formatted and then placed in the error window.
9.  We then take grab the text from the outputted _data.ptx_ (from _nvcc.exe_) and compare it to the PTX already in the window using a `diff` algorithm. The final results of the `diff` function is the new PTX with what changed in the form of comments. I chose to put the change information in comments so that if the text is copied to another program, it will still run.
10.  Next, we store the position of the scrollbars and caret location for the PTX/SASS window. This is needed because after we re-fill the output window with text, we are going to want to restore these.
11.  Next we grab the line information from the PTX and store that. The line numbers will be needed later to draw the connecting lines. The line information is in the form of "`.loc # ## #`" statements. Any line information is then deleted from the PTX so that it is not displayed.
12.  Do some cleanup on the PTX to make it look all nice and dandy.
13.  Next, we draw the visual code lines.
    1.  Previously, we saved the line number information for each location specified in the output PTX file. Example: On line 45 of the PTX we might have had a `.loc 1 20 1`. The `20` here would be the source line so a line would be drawn from line 20 in the source to line 45 in the PTX window.
    2.  Next, we get the indentation for each line. This is done by counting the whitespace (spaces/tabs) before each word. This is needed so the lines start or end where the code starts instead of just at the beginning of the line.
    3.  Using the textbox height/width plus the current scroll positions for each window plus the indentation and line number of each line, we then draw the lines.
14.  Finally, we restore the scroll positions and caret location.

## <a id="Features" name="Features">Features</a>

### Visual Code Lines

These lines match up the Cuda source code to the PTX output. They help the programmer quickly identify what Cuda code matches up with what PTX. This function can be enabled or disabled by clicking the lines icon in the top of the PTX window.

![50_visual_code_lines](https://cloud.githubusercontent.com/assets/10804507/21625198/711e0b00-d1bf-11e6-8ae0-5de854ca2d6b.png)

### Auto PTX Refresh

<div class="sidebar-right">**Tip**: When Auto PTX is turned off, a recompile can be forced by clicking "Ready".</div>

When needed, the application will automatically re-generate the PTX code. It does not do this on each text change in the source window but rather when the stuff that matters changes. Many items are stripped from the source text that do not impact the output such as comments or spaces. The Auto Update function can be enabled or disabled by clicking the auto update icon in the top of the PTX window.

### Built-in Diff utility

Each time the output window updates, this will automatically run a differencing algorithm each time the PTX output changes. The notes are added in such a way that it does not impact runnability of the code. I decided to add the `diff` information inside of a comments in the event the user wants to copy and paste the code. I came up with a system of using `//` style comments on deleted lines and a `/*new*/` comment for new comments. The `//` comments disable the entire line while the `/*new*/` does not.

<div class="sidebar-right">**Tip**: <span style="background-color: rgb(255, 255, 239)">To view differencing over several changes, disable the auto compile, make the changes, then click "Ready" to force a new re-compile.</span></div>

![60_windiff](https://cloud.githubusercontent.com/assets/10804507/21625197/711def4e-d1bf-11e6-9d74-828ea168febc.png)

### Single-Click Multiple Highlighting (new in 2016)

Just click on any register or word in the PTX window and it will highlight all instances of that item.  Click on another and it will highlight those as well with a different color.  Click on any highlighted item and it will un-highlight all instances of that item.  With just three click the following can be achieved: 

![quick_search_multiple_highlighting](https://cloud.githubusercontent.com/assets/10804507/21625206/713632de-d1bf-11e6-939c-0c9b5dc687d9.PNG)

### Syntax Highlighting and Output Formatting

The ScintillaNET textbox control by Jacob Slusser has some convenient text highlighting abilities that visually helps when viewing code. Originally, this started out as a plain textbox, then moved to another 3<sup>rd</sup> party control and then finally to the ScintillaNET control. This results in more colorful and cleaner looking code.

Besides the text highlighting, the text in the output window is formatted so it’s a little cleaner. Things like compiler information and header information are removed:

*   remove unneeded comment
*   remove unneeded id: comment
*   remove empty "//" comments
*   shorten __cudaparam_
*   shorten labels
*   remove .loc 15 lines (i.e. “.loc 3 3431 3”)
*   remove "%" in front of registers (New as of Jan. 2016)
*   remove "// Inline" lines (New as of Jan. 2016)
*   remove .file    1 "C:\\....." (New as of Jan. 2016)

Example of highlighted and cleaned up output formatting is as follows:

<div class="sidebar-right">**Note**: <span style="background-color: rgb(255, 255, 239)">Since 2009, NVidia has done a good job on cleaning up the PTX output. Labels are cleaner, padding has been added to make the registers line up and more.</span></div>

![65_syntax_highlighting](https://cloud.githubusercontent.com/assets/10804507/21625201/712efcee-d1bf-11e6-950c-6fe9d21ae348.png)

### Online Error/Warning Search

Often when running across an error, it is helpful to do a quick online search. I found I was often opening a browser and then copying and pasting the error in to a search box. This was not efficient so I added a search online function. At the time, I think this was one of the first of its kind but since it was released in 2009, I have seen other IDEs have this.

![70_online_error-warning_search](https://cloud.githubusercontent.com/assets/10804507/21625202/712f7282-d1bf-11e6-90c5-11fdf35557b2.png)

## Points of Interest

I had a little fun creating this. This is probably why so much time was put into this.

Getting the code lines to work was exciting for me. I believe the visual code lines might have been one of the first of their kind when I built this in 2009 but I am not sure. This was a wild idea I had and I was not sure if I could get it working. Drawing moving lines on the screen is not that easy as I found out as there always seemed to be some side effects. Drawing the spline was the easy part but all the miscellaneous stuff like cleaning it up was more difficult. Another difficult part was calculating the location in the text box. The textbox line height and line number must be known for each spline drawn. I’m not a graphics developer so I am just happy to get it to work! The visual lines turned out better than expected and are fun to play with.

At the time, I dreamed up many different “line” ideas to help break down the assembly but none of the others have been implemented yet:

**Note: These other features have NOT been added to CudaPAD. (at least not at this time)**

*   Draw curved lines that show jumps. Upward jumps are in a lighter color and downward jumps are in a darker color.
    ![75_asmjumplines](https://cloud.githubusercontent.com/assets/10804507/21625203/712fedd4-d1bf-11e6-8ea0-64c2ec3224b3.png)
*   Click on a register and it would display lines where a register impacts. Dark lines for the actual places the register is used. Gray for registers it impacts. And light gray for registers it impacts after two instructions. This would have been similar to Excel’s Trace Precedents / Trace Dependents function.
    ![80_register_tracing](https://cloud.githubusercontent.com/assets/10804507/21625204/713013b8-d1bf-11e6-947a-1b16c9301d60.png)
*   One other feature that I wanted to create but never got a chance to would have been a registers used function. This really helps understand where a kernel is maxing out on the register usage and often limits a kernel. When a register is used for the last time, it is freed after that instruction.
    ![95_register_usage](https://cloud.githubusercontent.com/assets/10804507/21625205/7131f6d8-d1bf-11e6-92c1-3637a29e51a6.png)

## Advantages of Viewing PTX/SASS

Here are some advantages of viewing PTX...

*   **Curiosity** - This is what I use it most for. Sometimes I just want to see what is going on at the lower levels and how small changes impact the code. This can be a very useful tool for trying to learn PTX/SASS and the Cuda compiler. 
*   **Software bug-** Trying to figure out that annoying bug. Is it a compiler bug or is it something with my code? Sometimes viewing the machine instructions can aid in understanding an unexpected result.
*   **Changing up a line or two often produces different results.** When there exists a kernel that might need some performance optimization, toying with different ways of doing the same thing can produce more efficient code. One example that comes to mind was I found that using a union the PTX would always result in local memory. This was a while ago so it might not be true anymore but here is the example:

    <pre lang="texT">local .align 4 .b8 someLocMem[4];  
    		....
    		st.local.s32      [someLocMem], someIntReg;  <--very expensive
    		ld.local.f32      someFloatReg, [someLocMem];  <--very expensive	</pre>

    However, when using something like:
*   <pre lang="text">"int strangeInt = *(int*) &somefloat;”
    		the output looks like this:
    		mov.b32 someFloatReg, someIntReg;</pre>

    This is easily spotted in CudaPAD because of the quick feedback and visual markers.
*   **Does the code do nothing?** Several times in the past, I realized that my kernel had a bug because when I changed or deleted some code nothing changed in the PTX output. I thought to myself, how could this be? The reason why PTX might not show up is because the compiler often simplifies out useless code that does not do anything. As I found out, this is more common then I expected because I ran into this a couple times. This is usually caused by a bug but it could also just be pointless code also. In most cases, code that is optimized out should either be removed or fixed. Noticing this can help find some hidden errors in a program.

Just as a word of caution, try not to go optimization crazy. Optimization does have its place for particular functions that get run often however optimization can make code less readable, awkward, and more difficult to maintain. Also, time should only be spent on code where a performance increase would have a large impact. There is much more on this subject that I will not get into.

## Videos <span style="color: rgb(255, 153, 0); font-size: 19px">(updated in 2016)</span>

Below is a quick[ tutorial video](https://youtu.be/SyPztdTjhmQ). The sub-menu options did not show properly in the video but I explain what I am clinking on so hopefully you can still follow along.

[![CudaPAD Tutorial](http://img.youtube.com/vi/SyPztdTjhmQ/0.jpg)](//www.youtube.com/watch?v=SyPztdTjhmQ "CudaPAD Tutorial")

CudaPAD won a [poster spot](http://on-demand.gputechconf.com/gtc/2016/posters/GTC_2016_Tools_and_Libraries_TL_03_P6247_WEB.pdf) at the 2016 GPU Technology Conference. Even better than that it was also selected as one of the top 20! At the conference, I gave a short presentation to about 100-150 people on April 4th 2016. 

[![CudaPAD Poster Video](http://img.youtube.com/vi/lgTO8y4-TVw/0.jpg)](//www.youtube.com/watch?v=lgTO8y4-TVw "CudaPAD Poster Video")

## Wish List

Here are some wish list items I have that may or may not be added in the future:

*   _Isolate_ the implementation code from interface code using the bridge pattern. While the GUI and code are somewhat split in different files right now, they are not really separable. It’s often good practice to split this up.
*   Add the ability to execute the code for timing purpose. Right now PTX can be visually looked at but not benchmarked.
*   Add a per-line register usage counter. Basically what this would require is to keep track of how many variables are being used on each PTX line. A GPU has a fixed number of registers and knowing where the register pressure is highest can help programmers balance their code. This is something I added in to my AMD GPU compiler, ASM4GCN, but have not added it here. 
*   Add jump lines to the PTX so one can _easily_ see where a jump statements lands.

## A Special Thanks to....

*   **[Diff functionality](http://www.codeproject.com/Articles/13326/An-O-ND-Difference-Algorithm-for-C)** - This is a nice drop-in C# file that provides quality diff functionality. Originally created by Eugene Myers in 1986; Converted in to C# by Matthias Herte. The mostly un-edited source is in the file Diff.cs.
*   **[ScintillaNET](https://github.com/jacobslusser/ScintillaNET)**- This nice tool provides the text highlighting for this project. It is a Windows Forms control, wrapper, and bindings for the versatile Scintilla source code editing component. It really adds a lot of life to this project.
*   **[nVidia](http://www.nvidia.com/)**- In 2016, CudaPAD won a spot on a [CudaPAD poster](http://on-demand.gputechconf.com/gtc/2016/posters/GTC_2016_Tools_and_Libraries_TL_03_P6247_WEB.pdf) at the 2016 GPU Technology Conference. Moreover, it was selected as _honorable mention_ (top 20). I [presented](https://youtu.be/lgTO8y4-TVw) it to an audance of around 100-150 people on a super large projector screen. It was a wonderful experance - one of the best I ever had. 

## History

*   Dec 2009 – Initially built
*   Jan 2013 – Changed the code textbox to use ScintillaNET for better syntax highlighting
*   Nov 2014 – Updated for NVidia Cuda 6.0/6.5
*   June 2015 – Code released to the public; changed to MIT License; updated for Cuda 6.5/7.0
*   Jan 2016 – Added a single-click multiple highlighting search feature; Updated for Cuda 7.0/7.5.
*   Jan 2017 – Verified okay with Cuda 8.0
