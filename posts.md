---
layout: default
---
{% for post in site.posts %}
----------------------------
### [{{ post.title }}]({{ post.url }})
> {{ post.excerpt }}

<sub><sup>[{{ post.url }}]({{ post.url }})</sup></sub>
{% endfor %}
