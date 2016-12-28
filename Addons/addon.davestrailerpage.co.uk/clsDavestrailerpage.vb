﻿' ################################################################################
' #                             EMBER MEDIA MANAGER                              #
' ################################################################################
' ################################################################################
' # This file is part of Ember Media Manager.                                    #
' #                                                                              #
' # Ember Media Manager is free software: you can redistribute it and/or modify  #
' # it under the terms of the GNU General Public License as published by         #
' # the Free Software Foundation, either version 3 of the License, or            #
' # (at your option) any later version.                                          #
' #                                                                              #
' # Ember Media Manager is distributed in the hope that it will be useful,       #
' # but WITHOUT ANY WARRANTY; without even the implied warranty of               #
' # MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the                #
' # GNU General Public License for more details.                                 #
' #                                                                              #
' # You should have received a copy of the GNU General Public License            #
' # along with Ember Media Manager.  If not, see <http://www.gnu.org/licenses/>. #
' ################################################################################

Imports EmberAPI
Imports NLog
Imports System.Text.RegularExpressions

Public Class Scraper

#Region "Fields"

    Shared logger As Logger = LogManager.GetCurrentClassLogger()

#End Region 'Fields

#Region "Methods"
    ''' <summary>
    ''' Scrapes available trailerlinks from http://www.davestrailerpage.co.uk/
    ''' </summary>
    ''' <remarks>Try to find trailerlinks for selected movie and add results to global trailerlist
    ''' 
    ''' 2014/10/05 Cocotus - First implementation
    ''' Steps: 
    ''' 1. Look for movie on  http://www.davestrailerpage.co.uk/ using originaltitle and download HTML of site! Example: http://www.davestrailerpage.co.uk/trailers_0to9.html
    ''' 2. Extract moviesection using Originaltitle/IMDBID 
    ''' 3. Further extract trailerlinks of moviesection.
    ''' 4. Add found trailerlink to global list
    ''' </remarks>
    Public Function Scrape_Movie(ByVal strOriginalTitle As String, ByVal strIMDB As String, ByVal tScrapeModifiers As Structures.ScrapeModifiers, ByVal tScrapeOptions As Structures.ScrapeOptions) As Interfaces.AddonResult
        Dim nAddonResult As New Interfaces.AddonResult

        Try
            If Not String.IsNullOrEmpty(strOriginalTitle) Then
                'constant URL-part of query
                Dim strBaseURL As String = String.Empty
                'dynamic URL-part of query
                Dim strSearchURL As String = String.Empty
                'webrequest-object of Ember used to scrape Webpages
                Dim sHTTP As New HTTP
                'retrieved JSON string
                Dim strJson As String = String.Empty
                'downloaded HTML of a webpage
                Dim strHtml As String = String.Empty
                'title to search for
                Dim strSearchTitle As String = strOriginalTitle.ToLower
                If strSearchTitle.StartsWith("the ") Then
                    strSearchTitle = strSearchTitle.Replace("the ", String.Empty)
                ElseIf strSearchTitle.StartsWith("an ") Then
                    strSearchTitle = strSearchTitle.Replace("an ", String.Empty)
                ElseIf strSearchTitle.StartsWith("a ") Then
                    strSearchTitle = strSearchTitle.Replace("a ", String.Empty)
                End If
                'TODO: Optional: originaltitle may contain not supported characters which must be filtered/cleaned first!?
                'searchtitle = StringUtils.RemovePunctuation(originaltitle)
                'URL to moviepage which contains downloadlinks
                Dim TrailerWEBPageURL As String = String.Empty
                'Step 1: Build URL to movie on http://www.davestrailerpage.co.uk/!
                'depending on beginning letter of movietitle we need to scrape different HTML site

                If Integer.TryParse(strSearchTitle(0), 0) Then
                    strSearchTitle = "0to9.html"
                ElseIf strSearchTitle(0).ToString.ToLower = "x" OrElse strSearchTitle(0).ToString.ToLower = "z" OrElse strSearchTitle(0).ToString.ToLower = "y" Then
                    strSearchTitle = "xyz.html"
                Else
                    strSearchTitle = (StringUtils.RemovePunctuation(strSearchTitle)).Replace(" ", String.Empty)(0) & ".html"
                End If
                'build URL
                strBaseURL = "http://www.davestrailerpage.co.uk/trailers_"
                strSearchURL = String.Concat(strBaseURL, strSearchTitle)
                'download HTML
                sHTTP = New HTTP
                strHtml = String.Empty
                strHtml = sHTTP.DownloadData(strSearchURL)
                sHTTP = Nothing

                'Step 2: Extract moviesection on downloaded HTML
                If Not String.IsNullOrEmpty(strHtml) Then
                    'example of returned HTML:
                    '<tr>
                    '<td><ul><li><b>X-Men Origins: Wolverine</b>
                    '	<ul><li><a href="http://www.x-menorigins.com/" target="_blank">Official Website</a> / <a href="http://www.imdb.com/title/tt0458525/" target="_blank">IMDB</a>
                    '		<li><b>Trailer 1</b>
                    '			<ul><li><a href="http://largeassets.myspacecdn.com/creative/hd/wolverine_trlA/48043679_480p.mov">480P</a>
                    '			<li><a href="http://largeassets.myspacecdn.com/creative/hd/wolverine_trlA/48043679_720p.mov">720P</a>
                    '			<li><a href="http://largeassets.myspacecdn.com/creative/hd/wolverine_trlA/48043679_1080p.mov">1080P</a>
                    '			</ul>
                    '		<li><b>Trailer 3</b>
                    '			<ul><li><a href="http://trailers.apple.com/movies/fox/wolverine/wolverine-clip_h640w.mov">SD</a>
                    '			<li><a href="http://trailers.apple.com/movies/fox/wolverine/wolverine-clip_h480p.mov">480P</a>
                    '			<li><a href="http://trailers.apple.com/movies/fox/wolverine/wolverine-clip_h720p.mov">720P</a>
                    '			<li><a href="http://trailers.apple.com/movies/fox/wolverine/wolverine-clip_h1080p.mov">1080P</a>
                    '			</ul>
                    '   </ul>
                    '</ul></td>
                    '</tr>
                    'marks the start of moviesection in downloaded HTML
                    Dim intstartposmovie As Integer = 0

                    'either use IMDBID or movietitle to search for movie on page
                    If Not String.IsNullOrEmpty(strIMDB) Then
                        intstartposmovie = strHtml.IndexOf(strIMDB)
                    End If
                    'if IMDBID search didn't help, look for title
                    If intstartposmovie < 1 Then
                        'search for movietitle 
                        Dim movieResults As MatchCollection = Nothing
                        Dim movietitlePattern As String = "<td><ul><li><b>(?<TITLE>.*?)</b>"
                        movieResults = Regex.Matches(strHtml, movietitlePattern, RegexOptions.Singleline)
                        If Not movieResults Is Nothing AndAlso movieResults.Count > 0 Then
                            'Go through each movietitle found on page and compare it with originaltitle using Levenshtein algorithm
                            Dim compareresult As Integer = 100
                            Dim comparestring As String = String.Empty
                            strSearchTitle = strOriginalTitle.ToLower
                            If strSearchTitle.StartsWith("the ") Then
                                strSearchTitle = strSearchTitle.Replace("the ", String.Empty)
                            ElseIf strSearchTitle.StartsWith("an ") Then
                                strSearchTitle = strSearchTitle.Replace("an ", String.Empty)
                            ElseIf strSearchTitle.StartsWith("a ") Then
                                strSearchTitle = strSearchTitle.Replace("a ", String.Empty)
                            End If
                            strSearchTitle = (StringUtils.RemovePunctuation(strSearchTitle)).Replace("the ", String.Empty).Replace(" ", String.Empty)
                            For Each movieresult As Match In movieResults
                                comparestring = movieresult.Groups(1).Value.ToLower
                                comparestring = comparestring.Replace("the ", String.Empty).Replace("an ", String.Empty).Replace("a ", String.Empty).Replace(", the", String.Empty).Replace(", a", String.Empty).Replace(", an", String.Empty)
                                compareresult = 100
                                comparestring = (StringUtils.RemovePunctuation(comparestring)).Replace(" ", String.Empty)
                                compareresult = StringUtils.ComputeLevenshtein(comparestring, strSearchTitle)

                                If (strOriginalTitle.Length <= 5 AndAlso compareresult = 0) OrElse (strOriginalTitle.Length > 5 AndAlso compareresult <= 2) Then
                                    logger.Debug("[" & strOriginalTitle & "] Davetrailer - Movie found! Title: " & movieresult.Groups(1).Value & " URL: " & strSearchURL)
                                    'set startpos
                                    intstartposmovie = strHtml.IndexOf(movieresult.Groups(1).Value)
                                    Exit For
                                End If
                            Next
                        End If
                    Else
                        logger.Debug("[" & strOriginalTitle & "] Davetrailer - Movie found! IMDBID: " & strIMDB & " URL: " & strSearchURL)
                    End If

                    'check if title was found
                    If intstartposmovie > 0 Then
                        'extracted HTML which contains information of movie (title,trailerlinks)
                        Dim strmoviesection As String = String.Empty
                        strmoviesection = (strHtml.Substring(intstartposmovie)).Remove(strHtml.Substring(intstartposmovie).IndexOf("</tr"))

                        ' Step 3: Further extract trailerlinks of moviesection
                        If Not String.IsNullOrEmpty(strmoviesection) Then
                            'now look for trailers in found moviesection

                            'moviesection can have 2 different structures
                            'Solution 1:
                            '<tr>
                            '	<td><ul><li><b>28 Hotel Rooms</b>
                            '		<ul><li><a href="http://28hotelrooms.com/" target="_blank">Official Website</a> / <a href="http://www.imdb.com/title/tt2124074/" target="_blank">IMDB</a>
                            '			<li><b>Trailer 1</b> <a href="http://trailers.apple.com/movies/independent/28hotelrooms/28hotelrooms-tsr1_h640w.mov">SD</a> | <a href="http://trailers.apple.com/movies/independent/28hotelrooms/28hotelrooms-tsr1_h480p.mov">480P</a> | <a href="http://trailers.apple.com/movies/independent/28hotelrooms/28hotelrooms-tsr1_h720p.mov">720P</a> | <a href="http://trailers.apple.com/movies/independent/28hotelrooms/28hotelrooms-tsr1_h1080p.mov">1080P</a>
                            '			<li><b>Trailer 2</b> <a href="http://trailers.apple.com/movies/independent/28hotelrooms/28hotelrooms-tlr2_h480p.mov">480P</a> | <a href="http://trailers.apple.com/movies/independent/28hotelrooms/28hotelrooms-tlr2_h720p.mov">720P</a> | <a href="http://trailers.apple.com/movies/independent/28hotelrooms/28hotelrooms-tlr2_h1080p.mov">1080P</a>
                            '		</ul>
                            '	</ul></td>
                            '</tr>
                            'Solution 2:
                            '<tr>
                            '	<td><ul><li><b>28 Days Later</b>
                            '	<ul><li><a href="http://trailers.apple.com/405/us/media/trailers/fox_searchlight/28_days_later/28_days_later_m480.mov">Teaser</a> SD </li>
                            '	<li><a href="http://trailers.apple.com/405/us/media/trailers/fox_searchlight/28_days_later/28_days_later-tlr_m480.mov">Trailer</a> SD </li>
                            '	</ul>
                            '</ul></td>
                            '</tr>

                            'solution 1 handling
                            Dim trailerResults As MatchCollection = Nothing
                            Dim titlePattern As String = "<li><b>(?<TITLE>.*?)</b>"
                            trailerResults = Regex.Matches(strmoviesection, titlePattern, RegexOptions.Singleline)
                            If Not trailerResults Is Nothing AndAlso trailerResults.Count > 0 Then
                                For Each trailerresult As Match In trailerResults
                                    'marks the start of trailer in moviesection
                                    Dim intstartpostrailer As Integer = 0
                                    intstartpostrailer = strmoviesection.IndexOf(trailerresult.Groups(1).Value)
                                    'extracted HTML which contains information of ONE trailer (url,quality)
                                    Dim strtrailersection As String = String.Empty
                                    strtrailersection = (strmoviesection.Substring(intstartpostrailer)).Remove(strmoviesection.Substring(intstartpostrailer).IndexOf("</ul"))
                                    If Not String.IsNullOrEmpty(strtrailersection) Then
                                        Dim trailerURLResults As MatchCollection = Nothing
                                        Dim trailerlinkPattern As String = "<a href=""(?<URL>.*?)"">(?<QUALITY>.*?)</a>"
                                        trailerURLResults = Regex.Matches(strtrailersection, trailerlinkPattern, RegexOptions.Singleline)
                                        If Not trailerURLResults Is Nothing AndAlso trailerURLResults.Count > 0 Then
                                            For Each nTrailerLink As Match In trailerURLResults
                                                Dim nTrailer As New MediaContainers.Trailer
                                                'trailer description
                                                nTrailer.Title = trailerresult.Groups(1).Value
                                                'trailer URLs
                                                nTrailer.URLVideoStream = nTrailerLink.Groups(1).Value
                                                nTrailer.URLWebsite = nTrailerLink.Groups(1).Value
                                                'trailer extension
                                                nTrailer.TrailerOriginal.Extention = IO.Path.GetExtension(nTrailer.URLVideoStream)
                                                'trailer source
                                                nTrailer.Source = "Davestrailer"
                                                '..and most important: trailer quality
                                                If nTrailerLink.Groups(2).Value.Contains("1080") Then
                                                    '1080p
                                                    nTrailer.Quality = Enums.TrailerVideoQuality.HD1080p
                                                ElseIf nTrailerLink.Groups(2).Value.Contains("720") Then
                                                    '720p
                                                    nTrailer.Quality = Enums.TrailerVideoQuality.HD720p
                                                Else
                                                    'other                     
                                                    nTrailer.Quality = Enums.TrailerVideoQuality.HQ480p
                                                End If
                                                ' Step 4: Add scraped trailer to global list
                                                nAddonResult.ScraperResult_Trailers.Add(nTrailer)
                                            Next
                                        End If
                                    End If
                                Next
                                'solution 2 handling
                            Else
                                Dim trailerURLResults As MatchCollection = Nothing
                                Dim trailerlinkPattern As String = "<a href=""(?<URL>.*?)"">(?<TITLE>.*?)</a>(?<QUALITY>.*?)</li"
                                strmoviesection = strmoviesection.Replace("\r\n", String.Empty).Replace("\n", String.Empty).Replace("\r", String.Empty)
                                trailerURLResults = Regex.Matches(strmoviesection, trailerlinkPattern, RegexOptions.Singleline)
                                If trailerURLResults Is Nothing OrElse trailerURLResults.Count < 1 Then
                                    trailerlinkPattern = "<a href=""(?<URL>.*?)"">(?<TITLE>.*?)</a>"
                                    trailerURLResults = Regex.Matches(strmoviesection, trailerlinkPattern, RegexOptions.Singleline)
                                End If
                                If Not trailerURLResults Is Nothing AndAlso trailerURLResults.Count > 0 Then
                                    For Each trailerlink As Match In trailerURLResults

                                        If trailerlink.Groups(1).Value.ToLower.Contains(".mov") OrElse trailerlink.Groups(1).Value.ToLower.Contains(".mp4") OrElse trailerlink.Groups(1).Value.ToLower.Contains(".flv") Then
                                            Dim trailer As New MediaContainers.Trailer
                                            'trailer description
                                            trailer.Title = trailerlink.Groups(2).Value
                                            'trailer URLs
                                            trailer.URLVideoStream = trailerlink.Groups(1).Value
                                            trailer.URLWebsite = trailerlink.Groups(1).Value
                                            'trailer extension
                                            trailer.TrailerOriginal.Extention = IO.Path.GetExtension(trailer.URLVideoStream)
                                            'trailer source
                                            trailer.Source = "Davestrailer"
                                            '..and most important: trailer quality
                                            If Not String.IsNullOrEmpty(trailerlink.Groups(3).Value) Then
                                                If trailerlink.Groups(3).Value.Contains("1080") Then
                                                    '1080p
                                                    trailer.Quality = Enums.TrailerVideoQuality.HD1080p
                                                ElseIf trailerlink.Groups(3).Value.Contains("720") Then
                                                    '720p
                                                    trailer.Quality = Enums.TrailerVideoQuality.HD720p
                                                Else
                                                    'other                     
                                                    trailer.Quality = Enums.TrailerVideoQuality.HQ480p
                                                End If
                                            Else
                                                If trailerlink.Groups(1).Value.Contains("1080") Then
                                                    '1080p
                                                    trailer.Quality = Enums.TrailerVideoQuality.HD1080p
                                                ElseIf trailerlink.Groups(1).Value.Contains("720") Then
                                                    '720p
                                                    trailer.Quality = Enums.TrailerVideoQuality.HD720p
                                                Else
                                                    'other                     
                                                    trailer.Quality = Enums.TrailerVideoQuality.HQ480p
                                                End If
                                            End If

                                            ' Step 4: Add scraped trailer to global list
                                            nAddonResult.ScraperResult_Trailers.Add(trailer)
                                        Else
                                            logger.Debug("[" & strOriginalTitle & "] Invalid trailerllink: " & trailerlink.Groups(1).Value)
                                        End If
                                    Next
                                Else
                                    logger.Debug("[" & strOriginalTitle & "] Davetrailer - No trailerlinks found on page! URL: " & strSearchURL)
                                End If
                            End If
                        Else
                            logger.Debug("[" & strOriginalTitle & "] Davetrailer - Moviesection NOT found on page! URL: " & strSearchURL)
                        End If
                    Else
                        logger.Debug("[" & strOriginalTitle & "] Davetrailer - Movie NOT found on page! URL: " & strSearchURL)
                    End If
                Else
                    logger.Warn("[" & strOriginalTitle & "] Davetrailer - HTML could not be downloaded! URL: " & strSearchURL)
                End If
            Else
                logger.Warn("[" & strOriginalTitle & "] Davetrailer - Original title is empty, no scraping of trailers possible!")
            End If

            'log scraperesults for user
            If nAddonResult.ScraperResult_Trailers.Count < 1 Then
                logger.Info("[" & strOriginalTitle & "] Davetrailer - NO trailer scraped!")
            Else
                logger.Info("[" & strOriginalTitle & "] Davetrailer - Scraped " & nAddonResult.ScraperResult_Trailers.Count & " trailer!")
            End If

        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
        End Try

        Return nAddonResult
    End Function

#End Region 'Methods

End Class