## Text decoration

```
Name           |Type       |Format                      
---------------|-----------|----------------------------
italic         |Standard   | *italic*
bold           |Standard   | **bold**
bold-italic    |Standard   | ***bold-italic***
strikethrough  |Standard   | ~~strikethrough~~
underline      |Proprietary| __underline__
color-text(1)  |Proprietary| %{color:red}colortext%
color-text(2)  |Proprietary| %{color:#00FF00}colortext%
bgcolor-text(1)|Proprietary| %{background:red}colortext%
bgcolor-text(2)|Proprietary| %{background:#FF00FF}colortext%
color&bgcolor  |Proprietary| %{color:red; background:yellow}colortext%
```
![image](https://github.com/user-attachments/assets/6deb8f0d-f1ef-43f6-848c-0928f2a17124)

## Text alignment

```
p<. arrange paragraph left-side.

p>. arrange paragraph right-side.

p=. arrange paragraph center.  
p=>. inner paragraph is ignored.
```
![image](https://github.com/user-attachments/assets/00c829c8-992a-4ec7-8838-6b0882e1e8c1)

## Blockquote

```
> ## heading in blockquote
> some tex in blockquote
> * listitem1
> * listitem2
```
![image](https://github.com/user-attachments/assets/d813b804-11de-4f4a-8769-e5a436dd6c1b)

## Code

```
  inline code: `this is inline.`
```
![image](https://github.com/user-attachments/assets/7ed36577-ce14-411c-bd01-a604ebb1186a)

````
```
#include <stdio.h>
int main()
{
    // printf() displays the string inside quotation
    printf("Hello, World!");
    return 0;
}
```
````
![image](https://github.com/user-attachments/assets/24e2f388-891e-4788-b0c2-a00bc98ba2d8)

## Header

```
# Heading1
## Heading2
### Heading3
#### Heading4
##### Heading5
###### Heading6
```
![image](https://github.com/user-attachments/assets/1016c1d6-88df-4d71-9675-8de23f684750)

## HorizontalRules

```
single line
 ---
two lines
 ===
bold line
***
bold with single
___
```
![image](https://github.com/user-attachments/assets/c446c4e9-cb0f-4d3d-a567-ec67afeafe93)

## Hyperlink

```
[hyperlinki1](http://example.com)
```
![image](https://github.com/user-attachments/assets/65176b96-2756-41cd-87cd-152a601a8ec6)

## List

```
list example

* listitem1
* listitem2
```
![image](https://github.com/user-attachments/assets/cb9dcaa1-c78e-4e81-bf60-301b696b9130)

```
#### alphabet-ol (Proprietary adaptations)
a. one
b. two

#### alphabet-ol (Proprietary adaptations)
A. one
B. two

#### roman-ol (Proprietary adaptations)
i, one
ii, two

#### roman-ol (Proprietary adaptations)
I, one
II, two
```
![image](https://github.com/user-attachments/assets/053a773e-4644-40d4-9d8f-f1995d99ea43)

## Table

```
|  column1   |  column2   |
|------------|------------|
| odd  cell1 | odd  cell2 |
| even cell1 | even cell2 |
| odd  cell1 | odd  cell2 |
| even cell1 | even cell2 |
```
![image](https://github.com/user-attachments/assets/64a4856f-85b6-4d35-93c7-f9a5f2001034)

```
| column1\nwith linebreak     | column2       |
|-----------------------------|---------------|
| text\nwith\nlinebreak       | text\\nnobreak|
```
![image](https://github.com/user-attachments/assets/0634d0a5-3480-41ac-9a38-61fc8726631f)

```
| column1     | column2       | column4       | column5  |
|-------------|---------------|---------------|----------|
|\2. colspan2                 |/3\2. row3&col2           |
|/2. rowspan2 |<. left-algn   |
              |=. center-algn |
| hoge        |>. right-align | hoge          | hoge     |

```
![image](https://github.com/user-attachments/assets/9c35e513-b2e7-402a-9689-95d10ba69ee1)

# DO NOT USE
````
```c
Crashes the Application.
```
````
