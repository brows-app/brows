---
layout: default
---
{% for post in site.posts %}
----------------------------
### [{{ post.title }}]({{ post.url }})
> {{ post.excerpt }}

<sub><sup>[{{ post.url | absolute_url}}]({{ post.url }})</sup></sub>
{% endfor %}
