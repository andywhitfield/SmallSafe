function ssInitialise() {
    let windowWidth = $(window).width();
    $(window).on('resize', function() {
        let newWindowWidth = $(window).width();
        if (Math.abs(newWindowWidth - windowWidth) < 2)
            return;

        windowWidth = newWindowWidth;
        $('aside').css('display', '');
        if ($('.nav-close:visible').length > 0) {
            $('.nav-close').css('display', '');
            $('.nav-show').css('display', '');
        }
    });
    $('.nav-show').click(function() {
        $('aside').fadeToggle('fast');
        $(this).hide();
        $('.nav-close').show();
    });
    $('.nav-close').click(function() {
        $('aside').hide();
        $(this).hide();
        $('.nav-show').show();
    });
    $('[data-href]').click(function(e) {
        window.location.href = $(this).attr('data-href');
        e.preventDefault();
        return false;
    });
    $('form[data-confirm]').submit(function(event) {
        if (!confirm($(this).attr('data-confirm'))) {
            event.preventDefault();
            return false;
        }
    });
    $('button[data-clipboard]').click(function() {
        ssCopyToClipboard($(this).attr('data-clipboard'));
    });

    $('form[name="generatepassword"]').submit(function(event) {
        $.getJSON('/api/generatepassword?genpwmin='+$('form[name="generatepassword"] input[name="genpwmin"]').val()+'&genpwmax='+$('form[name="generatepassword"] input[name="genpwmax"]').val()+'&genpwnums='+($('form[name="generatepassword"] input[name="genpwnums"]').is(':checked') ? 'true' : 'false')+'&genpwallchars='+($('form[name="generatepassword"] input[name="genpwallchars"]').is(':checked') ? 'true' : 'false')+'')
            .done(function(data) {
                $('.generated-passwords').empty();
                data.forEach(pw => {
                    let $newPw = $('.generated-passwords').append('<div><button title="Copy password to the clipboard"><img src="/images/copy.png" height="15" width="15" /></button> <input type="text" /></div>');
                    $('div:last button', $newPw).attr('data-clipboard', pw);
                    $('div:last input', $newPw).val(pw);
                });
                $('.generated-passwords button[data-clipboard]').click(function() {
                    ssCopyToClipboard($(this).attr('data-clipboard'));
                });
            })
            .fail(function() {
                $('.generated-passwords').empty();
                $('.generated-passwords').append('<div>Sorry, could not generate passwords. Please try again later.</div>');
            });

        event.preventDefault();
        return false;
    });

    $('ul.ss-list').sortable({
        handle: '.ss-list-item-drag-handle',
        isValidTarget: function(item, container) {
            return container.el[0].className.includes('ss-list-item');
        },
        onDrop: function(item, container, _super, event) {
            _super(item, container, event);

            let groupMoved = item.attr('data-group');
            let entryMoved = item.attr('data-entry');
            if (typeof groupMoved !== 'undefined' && typeof entryMoved !== 'undefined') {
                let prevEntry = item.prev('li').attr('data-entry');

                console.log('moving group/entry ' + groupMoved + '/' + entryMoved + ' to be after ' + prevEntry);

                if (typeof prevEntry === 'undefined')
                    prevEntry = null;

                console.log('moving group ' + groupMoved + ' entry ' + entryMoved + ' to be after ' + prevEntry);
                $.post('/api/group/' + groupMoved + '/entry/' + entryMoved + '/move', { prevEntryId: prevEntry })
                    .done(function() { console.log('entry move successful'); })
                    .fail(function() { window.location.reload(); });
            } else if (typeof groupMoved !== 'undefined') {
                let prevGroup = item.prev('li').attr('data-group');
                if (typeof prevGroup === 'undefined')
                    prevGroup = null;

                console.log('moving group ' + groupMoved + ' to be after ' + prevGroup);
                $.post('/api/group/' + groupMoved + '/move', { prevGroupId: prevGroup })
                    .done(function() { console.log('group move successful'); })
                    .fail(function() { window.location.reload(); });
            }
        }
    });
}

function ssInitialiseSafeEntry() {
    $('.ss-visible-value').hide();
    $('button.ss-hidden-value:first-child').click(function() {
        let $entryGroup = $(this).parentsUntil('li.ss-list-item').parent();
        let $groupId = $entryGroup.attr('data-group');
        let $entryId = $entryGroup.attr('data-entry');

        $.getJSON('/api/group/' + $groupId + '/entry/' + $entryId)
            .done(function(data) {
                $('textarea', $entryGroup).val(data.value);
                $('.ss-hidden-value', $entryGroup).hide();
                $('.ss-visible-value', $entryGroup).show();
            })
            .fail(function() { window.location.reload(); });
    });
    $('button.ss-hidden-value:nth-last-child(2)').click(function() {
        let $entryGroup = $(this).parentsUntil('li.ss-list-item').parent();
        let $groupId = $entryGroup.attr('data-group');
        let $entryId = $entryGroup.attr('data-entry');

        $.getJSON('/api/group/' + $groupId + '/entry/' + $entryId)
            .done(function(data) {
                $('textarea', $entryGroup).val(data.value);
                $('button.ss-hidden-value:nth-last-child(2)', $entryGroup).hide();
                $('button.ss-visible-value:last-child', $entryGroup).show();
            })
            .fail(function() { window.location.reload(); });
    });
    $('button.ss-visible-value:nth-child(2)').click(function() {
        let $entryGroup = $(this).parentsUntil('li.ss-list-item');
        $('.ss-visible-value', $entryGroup).hide();
        $('textarea', $entryGroup).val('');
        $('.ss-hidden-value', $entryGroup).show();
    });
    $('button.ss-visible-value:last-child').click(function() {
        let $entryGroup = $(this).parentsUntil('li.ss-list-item');
        ssCopyToClipboard($('textarea', $entryGroup).val());
        $('.ss-visible-value', $entryGroup).hide();
        $('textarea', $entryGroup).val('');
        $('.ss-hidden-value', $entryGroup).show();
    });
}

function ssCopyToClipboard(text) {
    if (!navigator.clipboard) {
        ssFallbackCopyToClipboard(text)
    } else {
        navigator.clipboard.writeText(text)
    }
}

function ssFallbackCopyToClipboard(text) {
    let textArea = document.createElement("textarea")
    textArea.value = text
    textArea.style.top = "0"
    textArea.style.left = "0"
    textArea.style.position = "fixed"
    document.body.appendChild(textArea)
    textArea.focus()
    textArea.select()

    try {
        document.execCommand('copy')
    } catch (err) {
        console.error('Unable to copy', err)
    }

    document.body.removeChild(textArea)
}