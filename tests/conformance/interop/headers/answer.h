#ifndef ANSWER_H
#define ANSWER_H

#define ANSWER (ANSWER_BASE + 2)

struct counter { int total; int step; };

static inline int answer(void) { return ANSWER; }
static inline int advance(struct counter* c) { c->total += c->step; return c->total; }

#endif
